// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;

namespace Cotton.Benchmark.Regression
{
    internal sealed class BenchmarkArtifactStore(string baselineDirectory, string resultsDirectory)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
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

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<BenchmarkRunDocument>(stream, JsonOptions, cancellationToken);
        }

        public async Task<string> SaveBaselineAsync(BenchmarkRunDocument runDocument, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_baselineDirectory);
            string path = GetBaselinePath(runDocument);
            await SaveJsonAsync(path, runDocument, cancellationToken);
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

        private static async Task SaveJsonAsync(string path, BenchmarkRunDocument runDocument, CancellationToken cancellationToken)
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, runDocument, JsonOptions, cancellationToken);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        }
    }
}
