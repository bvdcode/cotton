// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Benchmark.Regression
{
    internal sealed class BenchmarkComparisonResult
    {
        public bool Passed { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = [];
    }

    internal sealed class BenchmarkRegressionComparer
    {
        private const double DurationRegressionRatio = 1.20;
        private const double DurationRegressionGraceMs = 10;
        private const double ThroughputRegressionRatio = 0.85;
        private const double ThroughputRegressionGraceMBps = 5;

        private static readonly string[] LowerIsBetterMetrics =
        [
            "P50DurationMs",
            "P95DurationMs",
            "AvgDurationMs"
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
                    if (TryGetPair(baselineResult, currentResult, metricName, out double baselineValue, out double currentValue)
                        && IsLowerIsBetterRegression(baselineValue, currentValue))
                    {
                        passed = false;
                        messages.Add(FormatRegression(currentResult.Name, metricName, baselineValue, currentValue, "higher"));
                    }
                }

                foreach (string metricName in HigherIsBetterMetrics)
                {
                    if (TryGetPair(baselineResult, currentResult, metricName, out double baselineValue, out double currentValue)
                        && IsHigherIsBetterRegression(baselineValue, currentValue))
                    {
                        passed = false;
                        messages.Add(FormatRegression(currentResult.Name, metricName, baselineValue, currentValue, "lower"));
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

        private static bool IsLowerIsBetterRegression(double baselineValue, double currentValue)
        {
            return currentValue > baselineValue * DurationRegressionRatio
                && currentValue - baselineValue > DurationRegressionGraceMs;
        }

        private static bool IsHigherIsBetterRegression(double baselineValue, double currentValue)
        {
            return currentValue < baselineValue * ThroughputRegressionRatio
                && baselineValue - currentValue > ThroughputRegressionGraceMBps;
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
