// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using System.Text;

namespace Cotton.Benchmark.Reporting
{
    /// <summary>
    /// Formats benchmark results in a compact summary format.
    /// </summary>
    public sealed class SummaryResultFormatter : IResultFormatter
    {
        /// <inheritdoc/>
        public string Format(IBenchmarkResult result)
        {
            var sb = new StringBuilder();

            if (result.IsSuccess)
            {
                sb.Append($"[?] {result.BenchmarkName,-40} ");
                
                if (result.Metrics.TryGetValue("AvgThroughput", out var throughput))
                {
                    sb.Append($"{throughput,15}");
                }
                else
                {
                    sb.Append($"{FormatDuration(result.TotalDuration),15}");
                }
            }
            else
            {
                sb.Append($"[?] {result.BenchmarkName,-40} FAILED: {result.ErrorMessage}");
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string FormatCollection(IEnumerable<IBenchmarkResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Summary:");
            sb.AppendLine(new string('?', 70));

            foreach (var result in results)
            {
                sb.AppendLine(Format(result));
            }

            sb.AppendLine(new string('?', 70));

            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess);
            var totalTime = TimeSpan.FromMilliseconds(results.Sum(r => r.TotalDuration.TotalMilliseconds));

            sb.AppendLine($"Total: {results.Count()} | Success: {successCount} | Failed: {failureCount} | Time: {FormatDuration(totalTime)}");
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
    }
}
