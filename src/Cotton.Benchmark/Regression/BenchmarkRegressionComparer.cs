// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkComparisonResult
    {
        public bool Passed { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = [];
    }

    internal class BenchmarkRegressionComparer
    {
        private const double DurationRegressionRatio = 1.20;
        private const double DurationRegressionGraceMs = 10;
        private const double MemoryRegressionGraceBytes = 16 * 1024 * 1024;
        private const double ThroughputRegressionRatio = 0.85;
        private const double ThroughputRegressionGraceMBps = 5;
        private const double MinimumDurationForThroughputGateMs = 100;
        private const int MinimumIterationsForPercentileGate = 5;

        private record RegressionTolerance(
            double DurationRatio,
            double DurationGraceMs,
            double MemoryGraceBytes,
            double ThroughputRatio,
            double ThroughputGraceMBps,
            int MinimumIterationsForPercentileGate,
            int MinimumIterationsForThroughputGate)
        {
            private static readonly RegressionTolerance Strict = new(
                DurationRegressionRatio,
                DurationRegressionGraceMs,
                MemoryRegressionGraceBytes,
                ThroughputRegressionRatio,
                ThroughputRegressionGraceMBps,
                BenchmarkRegressionComparer.MinimumIterationsForPercentileGate,
                BenchmarkRegressionComparer.MinimumIterationsForPercentileGate);

            private static readonly RegressionTolerance Quick = new(
                2.00,
                50,
                MemoryRegressionGraceBytes,
                0.50,
                ThroughputRegressionGraceMBps,
                BenchmarkRegressionComparer.MinimumIterationsForPercentileGate,
                BenchmarkRegressionComparer.MinimumIterationsForPercentileGate);

            public static RegressionTolerance ForProfile(string profile)
            {
                return profile.Equals("quick", StringComparison.OrdinalIgnoreCase) ? Quick : Strict;
            }
        }

        private static readonly string[] LowerIsBetterMetrics =
        [
            "P50DurationMs",
            "P95DurationMs",
            "AvgDurationMs",
            "AvgManagedAllocatedBytes",
            "MaxManagedAllocatedBytes",
            "MaxWorkingSetBytes",
            "MaxPeakWorkingSetBytes"
        ];

        private static readonly string[] HigherIsBetterMetrics =
        [
            "AvgThroughputMBps",
            "MinThroughputMBps"
        ];

        public BenchmarkComparisonResult Compare(BenchmarkRunDocument baseline, BenchmarkRunDocument current)
        {
            var baselineByName = baseline.Results.ToDictionary(x => x.Name, StringComparer.Ordinal);
            var messages = new List<string>();
            bool passed = true;
            var tolerance = RegressionTolerance.ForProfile(current.Profile);

            foreach (var currentResult in current.Results)
            {
                if (!currentResult.Succeeded)
                {
                    passed = false;
                    messages.Add($"{currentResult.Name}: benchmark failed: {currentResult.ErrorMessage}");
                    continue;
                }

                if (!baselineByName.TryGetValue(currentResult.Name, out var baselineResult))
                {
                    messages.Add($"{currentResult.Name}: no baseline yet; run with --update-baseline after reviewing the result.");
                    continue;
                }

                foreach (string metricName in LowerIsBetterMetrics)
                {
                    if (ShouldCompareMetric(baselineResult, currentResult, metricName, tolerance)
                        && TryGetPair(baselineResult, currentResult, metricName, out double baselineValue, out double currentValue)
                        && IsLowerIsBetterRegression(metricName, baselineValue, currentValue, tolerance))
                    {
                        passed = false;
                        messages.Add(FormatRegression(currentResult.Name, metricName, baselineValue, currentValue, "higher"));
                    }
                }

                if (IsStableEnoughForThroughputGate(baselineResult, currentResult, tolerance))
                {
                    foreach (string metricName in HigherIsBetterMetrics)
                    {
                        if (TryGetPair(baselineResult, currentResult, metricName, out double baselineValue, out double currentValue)
                            && IsHigherIsBetterRegression(baselineValue, currentValue, tolerance))
                        {
                            passed = false;
                            messages.Add(FormatRegression(currentResult.Name, metricName, baselineValue, currentValue, "lower"));
                        }
                    }
                }
            }

            if (passed && messages.Count == 0)
            {
                messages.Add("No performance regressions detected.");
            }

            return new BenchmarkComparisonResult
            {
                Passed = passed,
                Messages = messages
            };
        }

        private static bool ShouldCompareMetric(
            BenchmarkResultSnapshot baseline,
            BenchmarkResultSnapshot current,
            string metricName,
            RegressionTolerance tolerance)
        {
            if (!metricName.Equals("P95DurationMs", StringComparison.Ordinal))
            {
                return true;
            }

            return TryGetPair(baseline, current, "Iterations", out double baselineIterations, out double currentIterations)
                && baselineIterations >= tolerance.MinimumIterationsForPercentileGate
                && currentIterations >= tolerance.MinimumIterationsForPercentileGate;
        }

        private static bool TryGetPair(
            BenchmarkResultSnapshot baseline,
            BenchmarkResultSnapshot current,
            string metricName,
            out double baselineValue,
            out double currentValue)
        {
            if (baseline.NumericMetrics.TryGetValue(metricName, out baselineValue)
                && current.NumericMetrics.TryGetValue(metricName, out currentValue)
                && baselineValue > 0
                && currentValue > 0)
            {
                return true;
            }

            baselineValue = 0;
            currentValue = 0;
            return false;
        }

        private static bool IsStableEnoughForThroughputGate(
            BenchmarkResultSnapshot baseline,
            BenchmarkResultSnapshot current,
            RegressionTolerance tolerance)
        {
            return TryGetPair(baseline, current, "Iterations", out double baselineIterations, out double currentIterations)
                && baselineIterations >= tolerance.MinimumIterationsForThroughputGate
                && currentIterations >= tolerance.MinimumIterationsForThroughputGate
                && TryGetPair(baseline, current, "AvgDurationMs", out double baselineDurationMs, out double currentDurationMs)
                && Math.Max(baselineDurationMs, currentDurationMs) >= MinimumDurationForThroughputGateMs;
        }

        private static bool IsLowerIsBetterRegression(string metricName, double baselineValue, double currentValue, RegressionTolerance tolerance)
        {
            double grace = metricName.EndsWith("Bytes", StringComparison.Ordinal)
                ? tolerance.MemoryGraceBytes
                : tolerance.DurationGraceMs;

            return currentValue > baselineValue * tolerance.DurationRatio
                && currentValue - baselineValue > grace;
        }

        private static bool IsHigherIsBetterRegression(double baselineValue, double currentValue, RegressionTolerance tolerance)
        {
            return currentValue < baselineValue * tolerance.ThroughputRatio
                && baselineValue - currentValue > tolerance.ThroughputGraceMBps;
        }

        private static string FormatRegression(
            string benchmarkName,
            string metricName,
            double baselineValue,
            double currentValue,
            string direction)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{benchmarkName}: {metricName} regressed; current {currentValue:F2} is {direction} than baseline {baselineValue:F2}.");
        }
    }
}
