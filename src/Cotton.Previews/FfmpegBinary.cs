// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;

namespace Cotton.Previews
{
    public static class FfmpegBinary
    {
        private const string FfmpegPathEnvironmentVariable = "COTTON_FFMPEG_PATH";
        private const string FfprobePathEnvironmentVariable = "COTTON_FFPROBE_PATH";

        public static string GetFfmpegPath() =>
            ResolveRequiredExecutable(FfmpegPathEnvironmentVariable, GetExecutableName("ffmpeg"));

        public static string GetFfprobePath() =>
            ResolveRequiredExecutable(FfprobePathEnvironmentVariable, GetExecutableName("ffprobe"));

        public static Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = GetFfmpegPath();
            _ = GetFfprobePath();
            return Task.CompletedTask;
        }

        public static async Task<double?> TryGetDurationSecondsAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<string> arguments = CreateProbeArguments(
                "default=nw=1:nk=1",
                "format=duration",
                url);

            string? raw = await RunFfprobeAsync(
                arguments,
                FfprobeOutputLimits.Default,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return raw is null ? null : ParsePositiveDuration(raw.Trim());
        }

        public static async Task<MediaProbeInfo?> TryGetMediaProbeAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<string> arguments = CreateProbeArguments(
                "json",
                "format=duration:stream=codec_name,codec_type",
                url);

            string? raw = await RunFfprobeAsync(
                arguments,
                FfprobeOutputLimits.Default,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(raw) ? null : FfprobeJsonParser.ParseMediaProbe(raw);
        }

        public static async Task<MediaMetadataInfo?> TryGetMediaMetadataAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default,
            MediaMetadataProbeLimits? limits = null)
        {
            MediaMetadataProbeLimits effectiveLimits = limits ?? MediaMetadataProbeLimits.Default;
            IReadOnlyCollection<string> arguments = CreateProbeArguments("json", MediaMetadataFields.ShowEntries, url);

            string? raw = await RunFfprobeAsync(
                arguments,
                effectiveLimits.Output,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(raw)
                ? null
                : FfprobeJsonParser.ParseMediaMetadata(raw, effectiveLimits);
        }

        private static IReadOnlyCollection<string> CreateProbeArguments(
            string outputFormat,
            string showEntries,
            Uri url)
        {
            return
            [
                "-v",
                "error",
                "-analyzeduration",
                "100M",
                "-probesize",
                "100M",
                "-of",
                outputFormat,
                "-show_entries",
                showEntries,
                url.ToString(),
            ];
        }

        private static string ResolveRequiredExecutable(string environmentVariable, string executableName)
        {
            return ResolveConfiguredExecutable(environmentVariable, executableName)
                ?? throw new FileNotFoundException(
                    $"{executableName} was not found. Install FFmpeg and add it to PATH, or set {environmentVariable} to its executable path.");
        }

        private static string? ResolveConfiguredExecutable(string environmentVariable, string executableName)
        {
            string? configured = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(configured))
            {
                return FindExecutableOnPath(executableName);
            }

            string trimmed = configured.Trim();
            if (File.Exists(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            string? fromPath = FindExecutableOnPath(trimmed);
            if (fromPath is not null)
            {
                return fromPath;
            }

            throw new FileNotFoundException($"{environmentVariable} points to '{trimmed}', but that executable was not found.");
        }

        private static string? FindExecutableOnPath(string executableName)
        {
            if (Path.IsPathRooted(executableName) || executableName.Contains(Path.DirectorySeparatorChar) || executableName.Contains(Path.AltDirectorySeparatorChar))
            {
                return File.Exists(executableName) ? Path.GetFullPath(executableName) : null;
            }

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string candidate = Path.Combine(directory.Trim('"'), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string GetExecutableName(string baseName) =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? $"{baseName}.exe" : baseName;

        private static async Task<string?> RunFfprobeAsync(
            IReadOnlyCollection<string> arguments,
            FfprobeOutputLimits outputLimits,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(outputLimits);
            await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);

            ProcessStartInfo startInfo = new()
            {
                FileName = GetFfprobePath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            BoundedProcessOutputReader stdoutReader = new(
                "stdout",
                outputLimits.MaxStandardOutputBytes);
            BoundedProcessOutputReader stderrReader = new(
                "stderr",
                outputLimits.MaxStandardErrorBytes);
            Task<string> stdoutTask = stdoutReader.ReadAsync(
                process.StandardOutput.BaseStream,
                CancellationToken.None);
            Task<string> stderrTask = stderrReader.ReadAsync(
                process.StandardError.BaseStream,
                CancellationToken.None);
            Task<bool> waitTask = PreviewProcess.WaitForExitAsync(process, timeout, cancellationToken);

            Task firstCompleted = await Task.WhenAny(
                waitTask,
                stdoutReader.LimitExceeded,
                stderrReader.LimitExceeded).ConfigureAwait(false);

            if (firstCompleted == stdoutReader.LimitExceeded
                || firstCompleted == stderrReader.LimitExceeded)
            {
                FfprobeOutputLimitExceededException exception = firstCompleted == stdoutReader.LimitExceeded
                    ? await stdoutReader.LimitExceeded.ConfigureAwait(false)
                    : await stderrReader.LimitExceeded.ConfigureAwait(false);

                await PreviewProcess.TerminateAsync(process).ConfigureAwait(false);
                await ((Task)waitTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await ((Task)Task.WhenAll(stdoutTask, stderrTask))
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                cancellationToken.ThrowIfCancellationRequested();
                throw exception;
            }

            bool completed;
            try
            {
                completed = await waitTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ((Task)Task.WhenAll(stdoutTask, stderrTask))
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                throw;
            }

            string stdout = await stdoutTask.ConfigureAwait(false);
            await stderrTask.ConfigureAwait(false);

            if (stdoutReader.LimitExceeded.IsCompletedSuccessfully)
            {
                throw await stdoutReader.LimitExceeded.ConfigureAwait(false);
            }

            if (stderrReader.LimitExceeded.IsCompletedSuccessfully)
            {
                throw await stderrReader.LimitExceeded.ConfigureAwait(false);
            }

            if (!completed || process.ExitCode != 0)
            {
                return null;
            }

            return stdout;
        }

        private static double? ParsePositiveDuration(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && value > 0
                    ? value
                    : null;
        }
    }
}
