// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotton.Benchmark.Regression
{
    internal class BenchmarkArtifactStore(string baselineDirectory, string resultsDirectory)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions SummaryJsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        private readonly string _baselineDirectory = baselineDirectory ?? throw new ArgumentNullException(nameof(baselineDirectory));
        private readonly string _resultsDirectory = resultsDirectory ?? throw new ArgumentNullException(nameof(resultsDirectory));

        public async Task<BenchmarkRunDocument?> LoadBaselineAsync(BenchmarkRunDocument runDocument, CancellationToken cancellationToken)
        {
            string path = GetBaselinePath(runDocument);
            if (!File.Exists(path))
            {
                return null;
            }

            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<BenchmarkRunDocument>(stream, JsonOptions, cancellationToken);
        }

        public async Task<string> SaveBaselineAsync(BenchmarkRunDocument runDocument, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_baselineDirectory);
            string path = GetBaselinePath(runDocument);
            await SaveJsonAsync(path, runDocument, cancellationToken);
            return path;
        }

        public async Task<string> SaveBaselineSummaryAsync(
            BenchmarkStoragePathSummaryDocument summaryDocument,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_baselineDirectory);
            string path = GetBaselineSummaryPath(summaryDocument);
            await SaveJsonAsync(path, summaryDocument, SummaryJsonOptions, cancellationToken);
            return path;
        }

        public async Task<string> SaveResultAsync(BenchmarkRunDocument runDocument, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_resultsDirectory);
            string fileName = string.Join(
                '.',
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),
                runDocument.HardwareKey,
                runDocument.Mode,
                runDocument.Profile,
                "json");

            string path = Path.Combine(_resultsDirectory, fileName);
            await SaveJsonAsync(path, runDocument, cancellationToken);
            return path;
        }

        public async Task<string> SaveResultSummaryAsync(
            string resultPath,
            BenchmarkStoragePathSummaryDocument summaryDocument,
            CancellationToken cancellationToken)
        {
            string resultDirectory = Path.GetDirectoryName(resultPath) ?? _resultsDirectory;
            string resultFileName = Path.GetFileNameWithoutExtension(resultPath);
            string path = Path.Combine(resultDirectory, $"{resultFileName}.storage-paths.json");
            await SaveJsonAsync(path, summaryDocument, SummaryJsonOptions, cancellationToken);
            return path;
        }

        public string GetBaselinePath(BenchmarkRunDocument runDocument)
        {
            string fileName = string.Join(
                '.',
                runDocument.HardwareKey,
                runDocument.Mode,
                runDocument.Profile,
                "json");

            return Path.Combine(_baselineDirectory, fileName);
        }

        public string GetBaselineSummaryPath(BenchmarkStoragePathSummaryDocument summaryDocument)
        {
            return Path.Combine(_baselineDirectory, $"{summaryDocument.HardwareId}.json");
        }

        private static Task SaveJsonAsync(
            string path,
            BenchmarkRunDocument runDocument,
            CancellationToken cancellationToken)
        {
            return SaveJsonAsync(path, runDocument, JsonOptions, cancellationToken);
        }

        private static async Task SaveJsonAsync<TDocument>(
            string path,
            TDocument document,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken)
        {
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, document, jsonOptions, cancellationToken);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        }
    }
}
