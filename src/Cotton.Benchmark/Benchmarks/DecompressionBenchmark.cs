// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Storage.Processors;
using System.Diagnostics;

namespace Cotton.Benchmark.Benchmarks
{
    public class DecompressionBenchmark : BenchmarkBase
    {
        private readonly byte[] _compressedData;
        private readonly int _originalSize;
        private readonly CompressionProcessor _processor;

        public DecompressionBenchmark(BenchmarkConfiguration configuration)
            : base(configuration)
        {
            // Use CompressionProcessor
            _processor = new CompressionProcessor(new FixedCompressionLevelProvider(configuration.CompressionLevel));

            // Pre-compress compressible data
            byte[] testData = TestDataGenerator.GenerateCompressibleText(configuration.DataSizeBytes);
            _originalSize = testData.Length;

            using MemoryStream inputStream = new MemoryStream(testData);
            Stream compressedStream = _processor.WriteAsync("test-uid", inputStream).Result;
            using MemoryStream outputStream = new MemoryStream();
            compressedStream.CopyTo(outputStream);
            _compressedData = outputStream.ToArray();
        }

        public override string Name => "Cotton.Storage Zstd Decompression";

        public override string Description => "Measures Cotton.Storage decompression throughput";

        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            await using MemoryStream inputStream = new MemoryStream(_compressedData);
            Stream outputStream = await _processor.ReadAsync("test-uid", inputStream);
            await outputStream.DisposeAsync();
        }

        protected override async Task<PerformanceMetrics> MeasureIterationAsync(CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            await using MemoryStream inputStream = new MemoryStream(_compressedData);
            Stream outputStream = await _processor.ReadAsync("test-uid", inputStream);

            // Read all decompressed data
            await using MemoryStream resultStream = new MemoryStream();
            await outputStream.CopyToAsync(resultStream, cancellationToken);

            stopwatch.Stop();

            return PerformanceMetrics.Create(_originalSize, stopwatch.Elapsed);
        }

        protected override Dictionary<string, object> AggregateMetrics(List<PerformanceMetrics> metrics)
        {
            Dictionary<string, object> baseMetrics = base.AggregateMetrics(metrics);
            baseMetrics["Implementation"] = "Cotton.Storage.Processors.CompressionProcessor";
            baseMetrics["CompressedSize"] = FormatBytes(_compressedData.Length);
            baseMetrics["CompressionRatio"] = $"{(double)_originalSize / _compressedData.Length:F2}x";
            baseMetrics["CompressionLevel"] = _configuration.CompressionLevel;
            return baseMetrics;
        }
    }
}
