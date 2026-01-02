// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;

namespace Cotton.Benchmark.Reporting
{
    /// <summary>
    /// Reports benchmark results to the console.
    /// </summary>
    public sealed class ConsoleReporter(IResultFormatter formatter) : IReporter
    {
        private readonly IResultFormatter _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

        /// <inheritdoc/>
        public Task ReportAsync(IEnumerable<IBenchmarkResult> results, CancellationToken cancellationToken = default)
        {
            var formattedResults = _formatter.FormatCollection(results);
            Console.WriteLine(formattedResults);
            return Task.CompletedTask;
        }
    }
}
