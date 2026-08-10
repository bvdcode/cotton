// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;

namespace Cotton.Previews
{
    internal static class FfmpegProcessRunner
    {
        public static async Task RunAsync(
            string arguments,
            Stream standardOutput,
            TimeSpan timeout,
            string operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(arguments);
            ArgumentNullException.ThrowIfNull(standardOutput);
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start ffmpeg for {operation}.");
            }

            Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(
                standardOutput,
                cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<bool> waitTask = FfmpegBinary.WaitForProcessAsync(
                process,
                timeout,
                cancellationToken);

            await Task.WhenAll(outputTask, errorTask, waitTask).ConfigureAwait(false);

            bool completed = await waitTask.ConfigureAwait(false);
            string standardError = await errorTask.ConfigureAwait(false);
            if (!completed)
            {
                string timeoutSeconds = timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
                throw new TimeoutException(
                    $"ffmpeg {operation} timed out after {timeoutSeconds} seconds.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"ffmpeg {operation} failed. exitCode={process.ExitCode}; stderr={standardError}");
            }
        }
    }
}
