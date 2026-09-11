// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using System.Globalization;

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkResultSnapshot
    {
        public string Name { get; init; } = string.Empty;

        public bool Succeeded { get; init; }

        public string? ErrorMessage { get; init; }

        public double TotalDurationMs { get; init; }

        public IReadOnlyDictionary<string, double> NumericMetrics { get; init; } = new Dictionary<string, double>();

        public IReadOnlyDictionary<string, string> TextMetrics { get; init; } = new Dictionary<string, string>();

        public static BenchmarkResultSnapshot FromResult(IBenchmarkResult result)
        {
            Dictionary<string, double> numericMetrics = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, string> textMetrics = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, object> metric in result.Metrics)
            {
                if (TryConvertToDouble(metric.Value, out double value))
                {
                    numericMetrics[metric.Key] = value;
                }
                else
                {
                    textMetrics[metric.Key] = Convert.ToString(metric.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            return new BenchmarkResultSnapshot
            {
                Name = result.BenchmarkName,
                Succeeded = result.IsSuccess,
                ErrorMessage = result.ErrorMessage,
                TotalDurationMs = result.TotalDuration.TotalMilliseconds,
                NumericMetrics = numericMetrics,
                TextMetrics = textMetrics
            };
        }

        private static bool TryConvertToDouble(object? value, out double converted)
        {
            switch (value)
            {
                case byte number:
                    converted = number;
                    return true;
                case short number:
                    converted = number;
                    return true;
                case int number:
                    converted = number;
                    return true;
                case long number:
                    converted = number;
                    return true;
                case float number:
                    converted = number;
                    return true;
                case double number:
                    converted = number;
                    return true;
                case decimal number:
                    converted = (double)number;
                    return true;
                case TimeSpan timeSpan:
                    converted = timeSpan.TotalMilliseconds;
                    return true;
                default:
                    converted = 0;
                    return false;
            }
        }
    }
}
