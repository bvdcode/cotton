// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Cotton.Benchmark.Infrastructure
{
    internal class PreviewMemoryWorkerResult
    {
        public bool Succeeded { get; init; }

        public string? ErrorMessage { get; init; }

        public int SmallPreviewBytes { get; init; }

        public int LargePreviewBytes { get; init; }

        public double DurationMs { get; init; }
    }

    internal static class PreviewMemoryWorker
    {
        private const string WorkerSwitch = "--preview-memory-worker";

        public static bool IsWorkerInvocation(IReadOnlyList<string> args)
        {
            return args.Contains(WorkerSwitch, StringComparer.Ordinal);
        }

        public static ProcessStartInfo CreateStartInfo(
            string sourcePath,
            string resultPath,
            int smallPreviewSize,
            int largePreviewSize)
        {
            string assemblyPath = Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Unable to locate benchmark assembly for preview memory worker.");

            var startInfo = new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            string? processPath = Environment.ProcessPath;
            string processName = Path.GetFileNameWithoutExtension(processPath ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(processPath)
                && !processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = processPath;
            }
            else
            {
                startInfo.FileName = processPath
                    ?? throw new InvalidOperationException("Unable to locate dotnet host for preview memory worker.");
                startInfo.ArgumentList.Add(assemblyPath);
            }

            startInfo.ArgumentList.Add(WorkerSwitch);
            startInfo.ArgumentList.Add("--source");
            startInfo.ArgumentList.Add(sourcePath);
            startInfo.ArgumentList.Add("--result");
            startInfo.ArgumentList.Add(resultPath);
            startInfo.ArgumentList.Add("--small-size");
            startInfo.ArgumentList.Add(smallPreviewSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--large-size");
            startInfo.ArgumentList.Add(largePreviewSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return startInfo;
        }

        public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            PreviewMemoryWorkerOptions options = Parse(args);
            var stopwatch = Stopwatch.StartNew();
            PreviewMemoryWorkerResult result;

            try
            {
                ImagePreviewGenerator generator = new();

                await using FileStream smallStream = OpenSource(options.SourcePath);
                byte[] smallPreview = await generator.GeneratePreviewWebPAsync(smallStream, options.SmallPreviewSize).ConfigureAwait(false);

                await using FileStream largeStream = OpenSource(options.SourcePath);
                byte[] largePreview = await generator.GeneratePreviewWebPAsync(largeStream, options.LargePreviewSize).ConfigureAwait(false);

                stopwatch.Stop();
                result = new PreviewMemoryWorkerResult
                {
                    Succeeded = true,
                    SmallPreviewBytes = smallPreview.Length,
                    LargePreviewBytes = largePreview.Length,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                stopwatch.Stop();
                result = new PreviewMemoryWorkerResult
                {
                    Succeeded = false,
                    ErrorMessage = ex.Message,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            await WriteResultAsync(options.ResultPath, result, cancellationToken).ConfigureAwait(false);
            return result.Succeeded ? 0 : 3;
        }

        private static FileStream OpenSource(string sourcePath)
        {
            return new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.SequentialScan | FileOptions.Asynchronous);
        }

        private static async Task WriteResultAsync(
            string resultPath,
            PreviewMemoryWorkerResult result,
            CancellationToken cancellationToken)
        {
            await using FileStream stream = new(
                resultPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous);

            await JsonSerializer.SerializeAsync(stream, result, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static PreviewMemoryWorkerOptions Parse(IReadOnlyList<string> args)
        {
            string? sourcePath = null;
            string? resultPath = null;
            int? smallSize = null;
            int? largeSize = null;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (arg.Equals(WorkerSwitch, StringComparison.Ordinal))
                {
                    continue;
                }

                switch (arg)
                {
                    case "--source":
                        sourcePath = ReadValue(args, ref i, arg);
                        break;
                    case "--result":
                        resultPath = ReadValue(args, ref i, arg);
                        break;
                    case "--small-size":
                        smallSize = int.Parse(ReadValue(args, ref i, arg), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "--large-size":
                        largeSize = int.Parse(ReadValue(args, ref i, arg), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    default:
                        throw new ArgumentException($"Unknown preview memory worker option: {arg}");
                }
            }

            return new PreviewMemoryWorkerOptions(
                Required(sourcePath, "--source"),
                Required(resultPath, "--result"),
                Required(smallSize, "--small-size"),
                Required(largeSize, "--large-size"));
        }

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Missing value for {optionName}.");
            }

            index++;
            return args[index];
        }

        private static T Required<T>(T? value, string optionName)
            where T : struct
        {
            return value ?? throw new ArgumentException($"Missing value for {optionName}.");
        }

        private static string Required(string? value, string optionName)
        {
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"Missing value for {optionName}.");
        }

        private readonly record struct PreviewMemoryWorkerOptions(
            string SourcePath,
            string ResultPath,
            int SmallPreviewSize,
            int LargePreviewSize);
    }
}
