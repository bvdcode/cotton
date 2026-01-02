// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using System.Text;

namespace Cotton.Benchmark.Reporting
{
    /// <summary>
    /// Formats benchmark results as a table.
    /// </summary>
    public sealed class TableResultFormatter : IResultFormatter
    {
        private const int NameWidth = 35;
        private const int ValueWidth = 25;

        /// <inheritdoc/>
        public string Format(IBenchmarkResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(new string('-', NameWidth + ValueWidth + 7));
            sb.AppendLine($"| {result.BenchmarkName.PadRight(NameWidth)} | {"Status".PadRight(ValueWidth)} |");
            sb.AppendLine(new string('-', NameWidth + ValueWidth + 7));

            if (result.IsSuccess)
            {
                sb.AppendLine($"| {"Result".PadRight(NameWidth)} | {"SUCCESS".PadRight(ValueWidth)} |");
                sb.AppendLine($"| {"Total Duration".PadRight(NameWidth)} | {FormatDuration(result.TotalDuration).PadRight(ValueWidth)} |");

                foreach (var metric in result.Metrics.OrderBy(m => m.Key))
                {
                    sb.AppendLine($"| {metric.Key.PadRight(NameWidth)} | {FormatValue(metric.Value).PadRight(ValueWidth)} |");
                }
            }
            else
            {
                sb.AppendLine($"| {"Result".PadRight(NameWidth)} | {"FAILED".PadRight(ValueWidth)} |");
                sb.AppendLine($"| {"Error".PadRight(NameWidth)} | {TruncateString(result.ErrorMessage ?? "Unknown", ValueWidth).PadRight(ValueWidth)} |");
                sb.AppendLine($"| {"Duration Before Failure".PadRight(NameWidth)} | {FormatDuration(result.TotalDuration).PadRight(ValueWidth)} |");
            }

            sb.AppendLine(new string('-', NameWidth + ValueWidth + 7));

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string FormatCollection(IEnumerable<IBenchmarkResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(new string('=', 70));
            sb.AppendLine("                    BENCHMARK RESULTS SUMMARY                   ");
            sb.AppendLine(new string('=', 70));

            foreach (var result in results)
            {
                sb.Append(Format(result));
            }

            // Summary statistics
            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess);
            var totalTime = TimeSpan.FromMilliseconds(results.Sum(r => r.TotalDuration.TotalMilliseconds));

            sb.AppendLine();
            sb.AppendLine(new string('=', 70));
            sb.AppendLine("                         SUMMARY                                ");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine($"Total Benchmarks:  {results.Count()}");
            sb.AppendLine($"Successful:        {successCount}");
            sb.AppendLine($"Failed:            {failureCount}");
            sb.AppendLine($"Total Time:        {FormatDuration(totalTime)}");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine();

            return sb.ToString();
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
            {
                return $"{duration.TotalMilliseconds:F2} ms";
            }
            else if (duration.TotalMinutes < 1)
            {
                return $"{duration.TotalSeconds:F2} sec";
            }
            else
            {
                return $"{duration.TotalMinutes:F2} min";
            }
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                TimeSpan ts => FormatDuration(ts),
                _ => TruncateString(value.ToString() ?? "", ValueWidth)
            };
        }

        private static string TruncateString(string str, int maxLength)
        {
            if (str.Length <= maxLength)
            {
                return str;
            }
            return str[..(maxLength - 3)] + "...";
        }
    }
}
