// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public static class FfmpegBinary
    {
        private const string FfmpegPathEnvironmentVariable = "COTTON_FFMPEG_PATH";
        private const string FfprobePathEnvironmentVariable = "COTTON_FFPROBE_PATH";
        private const string FfmpegDirectoryEnvironmentVariable = "COTTON_FFMPEG_DIR";
        private const string CacheDirectoryName = "cotton-ffmpeg";
        private const string MediaMetadataShowEntries =
            "format=duration:" +
            "format_tags=title,artist,album,album_artist,albumartist,album artist,composer,performer," +
            "track,tracknumber,track_number,disc,discnumber,disc_number,date,creation_time,year,genre:" +
            "stream=codec_name,codec_type,width,height";

        private static readonly SemaphoreSlim DownloadGate = new(1, 1);
        private static string? _ffmpegPath;
        private static string? _ffprobePath;

        public static string GetFfmpegPath() =>
            _ffmpegPath ?? ResolveExistingExecutable(FfmpegPathEnvironmentVariable, GetExecutableName("ffmpeg")) ?? GetDownloadedExecutablePath("ffmpeg");

        public static string GetFfprobePath() =>
            _ffprobePath ?? ResolveExistingExecutable(FfprobePathEnvironmentVariable, GetExecutableName("ffprobe")) ?? GetDownloadedExecutablePath("ffprobe");

        public static async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
        {
            if (TryResolveInstalledBinaries(out string ffmpegPath, out string ffprobePath))
            {
                _ffmpegPath = ffmpegPath;
                _ffprobePath = ffprobePath;
                return;
            }

            await DownloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (TryResolveInstalledBinaries(out ffmpegPath, out ffprobePath))
                {
                    _ffmpegPath = ffmpegPath;
                    _ffprobePath = ffprobePath;
                    return;
                }

                string downloadDirectory = GetDownloadDirectory();
                Directory.CreateDirectory(downloadDirectory);

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, downloadDirectory).ConfigureAwait(false);

                ffmpegPath = Path.Combine(downloadDirectory, GetExecutableName("ffmpeg"));
                ffprobePath = Path.Combine(downloadDirectory, GetExecutableName("ffprobe"));

                if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath))
                {
                    throw new FileNotFoundException($"ffmpeg download did not produce both expected binaries in '{downloadDirectory}'.");
                }

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    await ChmodExecutableAsync(ffmpegPath, cancellationToken).ConfigureAwait(false);
                    await ChmodExecutableAsync(ffprobePath, cancellationToken).ConfigureAwait(false);
                }

                _ffmpegPath = ffmpegPath;
                _ffprobePath = ffprobePath;
            }
            finally
            {
                DownloadGate.Release();
            }
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
            IReadOnlyCollection<string> arguments = CreateProbeArguments(
                "json",
                MediaMetadataShowEntries,
                url);

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

        private static bool TryResolveInstalledBinaries(out string ffmpegPath, out string ffprobePath)
        {
            ffmpegPath = string.Empty;
            ffprobePath = string.Empty;

            string? resolvedFfmpeg = ResolveConfiguredExecutable(FfmpegPathEnvironmentVariable, GetExecutableName("ffmpeg"));
            string? resolvedFfprobe = ResolveConfiguredExecutable(FfprobePathEnvironmentVariable, GetExecutableName("ffprobe"));

            if (resolvedFfmpeg is null || resolvedFfprobe is null)
            {
                return false;
            }

            ffmpegPath = resolvedFfmpeg;
            ffprobePath = resolvedFfprobe;
            return true;
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

        private static string? ResolveExistingExecutable(string environmentVariable, string executableName)
        {
            try
            {
                return ResolveConfiguredExecutable(environmentVariable, executableName);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
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

        private static string GetDownloadedExecutablePath(string baseName) =>
            Path.Combine(GetDownloadDirectory(), GetExecutableName(baseName));

        private static string GetDownloadDirectory()
        {
            string? configured = Environment.GetEnvironmentVariable(FfmpegDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }

            string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, CacheDirectoryName);
            }

            return Path.Combine(Path.GetTempPath(), CacheDirectoryName);
        }

        private static string GetExecutableName(string baseName) =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? $"{baseName}.exe" : baseName;

        private static async Task ChmodExecutableAsync(string path, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                ArgumentList = { "+x", path },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process process = new Process { StartInfo = startInfo };
            if (process.Start())
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

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
            Task<bool> waitTask = WaitForProcessAsync(process, timeout, cancellationToken);

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

                await TerminateProcessAsync(process).ConfigureAwait(false);
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

        internal static async Task<bool> WaitForProcessAsync(
            Process process,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task timeoutTask = Task.Delay(effectiveTimeout, cancellationToken);
            Task completedTask = await Task.WhenAny(exitTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == exitTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await exitTask.ConfigureAwait(false);
                return true;
            }

            await TerminateProcessAsync(process).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        internal static async Task TerminateProcessAsync(Process process)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
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
