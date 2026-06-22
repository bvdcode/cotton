// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Benchmark.Abstractions;
using Cotton.Benchmark.Infrastructure;
using Cotton.Benchmark.Models;
using Cotton.Previews;
using System.Diagnostics;
using System.Text.Json;

namespace Cotton.Benchmark.Benchmarks
{
    /// <summary>
    /// Measures memory required by real raster preview generation on large decoded images.
    /// </summary>
    internal class ImagePreviewMemoryBenchmark(BenchmarkConfiguration configuration, BenchmarkProfile profile) : IBenchmark
    {
        private static readonly TimeSpan WorkerMemorySampleInterval = TimeSpan.FromMilliseconds(10);
        private readonly BmpImageSpec _imageSpec = CreateImageSpec(profile);
        private readonly BenchmarkConfiguration _configuration = configuration;

        /// <inheritdoc />
        public string Name => "Image Preview Memory Capacity";

        /// <inheritdoc />
        public string Description => "Generates a deterministic BMP source and measures ImagePreviewGenerator memory use in an isolated worker process";

        /// <inheritdoc />
        public async Task<IBenchmarkResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"cotton-preview-benchmark-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(tempDirectory);
                string sourcePath = Path.Combine(tempDirectory, "source.bmp");
                string resultPath = Path.Combine(tempDirectory, "result.json");

                var sourceStopwatch = Stopwatch.StartNew();
                await BmpTestImageWriter.WriteAsync(sourcePath, _imageSpec, cancellationToken).ConfigureAwait(false);
                sourceStopwatch.Stop();

                WorkerObservation observation = await RunWorkerAsync(sourcePath, resultPath, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                Dictionary<string, object> metrics = CreateMetrics(sourceStopwatch.Elapsed, observation);
                return BenchmarkResult.Success(Name, stopwatch.Elapsed, metrics);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                stopwatch.Stop();
                return BenchmarkResult.Failure(Name, ex.Message, stopwatch.Elapsed);
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private Dictionary<string, object> CreateMetrics(TimeSpan sourceGenerationDuration, WorkerObservation observation)
        {
            PreviewMemoryWorkerResult result = observation.Result;
            var metrics = new Dictionary<string, object>
            {
                ["ProfileDataSize"] = FormatBytes(_configuration.DataSizeBytes),
                ["SourceFormat"] = "BMP 24-bit",
                ["SourceDimensions"] = _imageSpec.Dimensions,
                ["SourceMegapixels"] = _imageSpec.Megapixels,
                ["SourceFileBytes"] = _imageSpec.FileSizeBytes,
                ["SourceFileSize"] = FormatBytes(_imageSpec.FileSizeBytes),
                ["EstimatedDecodedRgbaBytes"] = _imageSpec.DecodedRgbaBytes,
                ["EstimatedDecodedRgbaSize"] = FormatBytes(_imageSpec.DecodedRgbaBytes),
                ["SourceGenerationMs"] = sourceGenerationDuration.TotalMilliseconds,
                ["SmallPreviewSize"] = PreviewGeneratorProvider.DefaultSmallPreviewSize,
                ["LargePreviewSize"] = PreviewGeneratorProvider.DefaultLargePreviewSize,
                ["WorkerExitCode"] = observation.ExitCode,
                ["WorkerSucceeded"] = result.Succeeded,
                ["WorkerDurationMs"] = result.DurationMs,
                ["WorkerObservedMaxWorkingSetBytes"] = observation.MaxWorkingSetBytes,
                ["WorkerObservedMaxWorkingSet"] = FormatBytes(observation.MaxWorkingSetBytes),
                ["SmallPreviewBytes"] = result.SmallPreviewBytes,
                ["LargePreviewBytes"] = result.LargePreviewBytes
            };

            if (!result.Succeeded)
            {
                metrics["WorkerError"] = result.ErrorMessage ?? "Worker exited without a structured error.";
            }

            if (!string.IsNullOrWhiteSpace(observation.StandardError))
            {
                metrics["WorkerStandardError"] = Truncate(observation.StandardError, 300);
            }

            metrics["CapacityConclusion"] = result.Succeeded
                ? $"{_imageSpec.Megapixels:F1} MP source succeeded; observed worker RSS peak {FormatBytes(observation.MaxWorkingSetBytes)}."
                : $"{_imageSpec.Megapixels:F1} MP source failed; observed worker RSS peak {FormatBytes(observation.MaxWorkingSetBytes)}.";

            return metrics;
        }

        private static async Task<WorkerObservation> RunWorkerAsync(
            string sourcePath,
            string resultPath,
            CancellationToken cancellationToken)
        {
            using Process process = new()
            {
                StartInfo = PreviewMemoryWorker.CreateStartInfo(
                    sourcePath,
                    resultPath,
                    PreviewGeneratorProvider.DefaultSmallPreviewSize,
                    PreviewGeneratorProvider.DefaultLargePreviewSize)
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start preview memory worker process.");
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<long> monitorTask = ObserveWorkingSetAsync(process, monitorCancellation.Token);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryKill(process);
                throw;
            }
            finally
            {
                await monitorCancellation.CancelAsync().ConfigureAwait(false);
            }

            long maxWorkingSetBytes = await ReadMonitorResultAsync(monitorTask).ConfigureAwait(false);
            string standardOutput = await stdoutTask.ConfigureAwait(false);
            string standardError = await stderrTask.ConfigureAwait(false);
            PreviewMemoryWorkerResult result = await ReadWorkerResultAsync(resultPath, process.ExitCode, standardOutput, standardError, cancellationToken)
                .ConfigureAwait(false);

            return new WorkerObservation(
                process.ExitCode,
                maxWorkingSetBytes,
                standardOutput,
                standardError,
                result);
        }

        private static async Task<long> ObserveWorkingSetAsync(Process process, CancellationToken cancellationToken)
        {
            long maxWorkingSetBytes = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (process.HasExited)
                    {
                        break;
                    }

                    process.Refresh();
                    maxWorkingSetBytes = Math.Max(maxWorkingSetBytes, process.WorkingSet64);
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                try
                {
                    await Task.Delay(WorkerMemorySampleInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return maxWorkingSetBytes;
        }

        private static async Task<long> ReadMonitorResultAsync(Task<long> monitorTask)
        {
            try
            {
                return await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        private static async Task<PreviewMemoryWorkerResult> ReadWorkerResultAsync(
            string resultPath,
            int exitCode,
            string standardOutput,
            string standardError,
            CancellationToken cancellationToken)
        {
            if (File.Exists(resultPath))
            {
                await using FileStream stream = File.OpenRead(resultPath);
                PreviewMemoryWorkerResult? result = await JsonSerializer.DeserializeAsync<PreviewMemoryWorkerResult>(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (result is not null)
                {
                    return result;
                }
            }

            return new PreviewMemoryWorkerResult
            {
                Succeeded = false,
                ErrorMessage = $"Worker exited with code {exitCode}. stdout={Truncate(standardOutput, 200)} stderr={Truncate(standardError, 200)}"
            };
        }

        private static BmpImageSpec CreateImageSpec(BenchmarkProfile profile)
        {
            return profile switch
            {
                BenchmarkProfile.Quick => new BmpImageSpec(4096, 3072),
                BenchmarkProfile.Standard => new BmpImageSpec(10000, 10000),
                BenchmarkProfile.Full => new BmpImageSpec(20000, 10000),
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported benchmark profile.")
            };
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private record WorkerObservation(
            int ExitCode,
            long MaxWorkingSetBytes,
            string StandardOutput,
            string StandardError,
            PreviewMemoryWorkerResult Result);
    }
}
