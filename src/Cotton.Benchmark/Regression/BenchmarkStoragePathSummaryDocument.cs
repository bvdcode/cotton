// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkStoragePathSummaryDocument
    {
        private const string ThroughputMetricName = "AvgThroughputMBps";
        private const string P50DurationMetricName = "P50DurationMs";
        private const string P95DurationMetricName = "P95DurationMs";
        private const string DataSizeMetricName = "DataSizeBytes";

        private static readonly BenchmarkStageMapping[] WriteStageMappings =
        [
            new("sha256", "SHA-256 hashing", "Hashing (SHA-256)"),
            new("zstdCompression", "Zstd compression", "Cotton.Storage Zstd Compression"),
            new("aesGcmEncryption", "AES-GCM encryption", "Cotton.Storage AES-GCM Encryption"),
            new("filesystemWrite", "Filesystem write", "Filesystem Backend Write")
        ];

        private static readonly BenchmarkStageMapping[] ReadStageMappings =
        [
            new("filesystemRead", "Filesystem read", "Filesystem Backend Read"),
            new("aesGcmDecryption", "AES-GCM decryption", "Cotton.Storage AES-GCM Decryption"),
            new("zstdDecompression", "Zstd decompression", "Cotton.Storage Zstd Decompression")
        ];

        private static readonly BenchmarkStageMapping WritePipelineMapping = new(
            "chunkUploadProcessing",
            "Chunk upload processing",
            "Chunk Upload Processing - Mixed content (SHA-256 + Compression + Encryption)");

        private static readonly BenchmarkStageMapping ReadPipelineMapping = new(
            "storagePipelineRead",
            "Storage pipeline read",
            "Storage Pipeline Read (Decryption + Decompression)");

        public int SchemaVersion { get; init; } = 1;

        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

        public string Mode { get; init; } = string.Empty;

        public string Profile { get; init; } = string.Empty;

        public string HardwareKey { get; init; } = string.Empty;

        public string HardwareId { get; init; } = string.Empty;

        public string GitCommit { get; init; } = string.Empty;

        public string Units { get; init; } = "MiB/s";

        public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

        public BenchmarkStoragePathGroup Write { get; init; } = new();

        public BenchmarkStoragePathGroup Read { get; init; } = new();

        public static BenchmarkStoragePathSummaryDocument Create(BenchmarkRunDocument runDocument)
        {
            Dictionary<string, BenchmarkResultSnapshot> resultsByName = runDocument.Results
                .Where(result => result.Succeeded)
                .ToDictionary(result => result.Name, StringComparer.Ordinal);

            return new BenchmarkStoragePathSummaryDocument
            {
                CreatedAtUtc = runDocument.CreatedAtUtc,
                Mode = runDocument.Mode,
                Profile = runDocument.Profile,
                HardwareKey = runDocument.HardwareKey,
                HardwareId = BenchmarkHardwareId.Create(runDocument),
                GitCommit = runDocument.GitCommit,
                Environment = runDocument.Environment,
                Write = CreateGroup(resultsByName, WriteStageMappings, WritePipelineMapping),
                Read = CreateGroup(resultsByName, ReadStageMappings, ReadPipelineMapping)
            };
        }

        private static BenchmarkStoragePathGroup CreateGroup(
            IReadOnlyDictionary<string, BenchmarkResultSnapshot> resultsByName,
            IReadOnlyList<BenchmarkStageMapping> stageMappings,
            BenchmarkStageMapping pipelineMapping)
        {
            BenchmarkStoragePathStage[] stages = stageMappings
                .Select(mapping => CreateStage(resultsByName, mapping))
                .Where(stage => stage is not null)
                .Cast<BenchmarkStoragePathStage>()
                .ToArray();

            BenchmarkStoragePathStage? pipeline = CreateStage(resultsByName, pipelineMapping);

            return new BenchmarkStoragePathGroup
            {
                Stages = stages,
                Pipeline = pipeline,
                LimitingStage = stages.MinBy(stage => stage.MibPerSecond)
            };
        }

        private static BenchmarkStoragePathStage? CreateStage(
            IReadOnlyDictionary<string, BenchmarkResultSnapshot> resultsByName,
            BenchmarkStageMapping mapping)
        {
            if (!resultsByName.TryGetValue(mapping.SourceBenchmark, out BenchmarkResultSnapshot? result)
                || !result.NumericMetrics.TryGetValue(ThroughputMetricName, out double throughput))
            {
                return null;
            }

            return new BenchmarkStoragePathStage
            {
                Key = mapping.Key,
                Label = mapping.Label,
                MibPerSecond = throughput,
                SourceBenchmark = mapping.SourceBenchmark,
                P50DurationMs = TryGetMetric(result, P50DurationMetricName),
                P95DurationMs = TryGetMetric(result, P95DurationMetricName),
                DataSizeBytes = TryGetMetric(result, DataSizeMetricName)
            };
        }

        private static double? TryGetMetric(BenchmarkResultSnapshot result, string metricName)
        {
            if (result.NumericMetrics.TryGetValue(metricName, out double value))
            {
                return value;
            }

            return null;
        }
    }
}
