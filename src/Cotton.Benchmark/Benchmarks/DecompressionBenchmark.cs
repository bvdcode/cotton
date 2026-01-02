// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Models;
using System.Diagnostics;
using ZstdSharp;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Benchmark for decompression performance using Zstd.
    /// </summary>
    public sealed class DecompressionBenchmark : BenchmarkBase
    {
        private readonly byte[] _compressedData;
        private readonly int _originalSize;

        public DecompressionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            var testData = GenerateTestData(configuration.DataSizeBytes);
            _originalSize = testData.Length;

            // Pre-compress data for decompression benchmark
            using var outputStream = new MemoryStream();
            using (var compressor = new CompressionStream(outputStream, level: configuration.CompressionLevel, leaveOpen: true))
            {
                compressor.Write(testData);
            }
            _compressedData = outputStream.ToArray();
        }

        /// <inheritdoc/>
        public override string Name => "Decompression (Zstd)";

        /// <inheritdoc/>
        public override string Description => "Tests Zstd decompression performance";

        /// <inheritdoc/>
        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using var inputStream = new MemoryStream(_compressedData);
            await using var decompressor = new DecompressionStream(inputStream);
            await using var outputStream = new MemoryStream();
            await decompressor.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var inputStream = new MemoryStream(_compressedData);
            await using var decompressor = new DecompressionStream(inputStream);
            await using var outputStream = new MemoryStream();
            await decompressor.CopyToAsync(outputStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_originalSize, stopwatch.Elapsed);
        }

        /// <inheritdoc/>
        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            var baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["CompressedSize"] = FormatBytes(_compressedData.Length);
            baseMetrics["CompressionRatio"] = $"{(double)_originalSize / _compressedData.Length:F2}x";
            return baseMetrics;
        }
    }
}
