// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    /// <summary>
    /// Media metadata extracted by ffprobe for preview and playback planning.
    /// </summary>
    public record MediaProbeInfo(double? DurationSeconds, string? VideoCodec, string? AudioCodec);

    /// <summary>
    /// Locates ffmpeg/ffprobe binaries used by preview generation and downloads them to a writable cache only when needed.
    /// </summary>
    public static class FfmpegBinary
    {
        private const string FfmpegPathEnvironmentVariable = "COTTON_FFMPEG_PATH";
        private const string FfprobePathEnvironmentVariable = "COTTON_FFPROBE_PATH";
        private const string FfmpegDirectoryEnvironmentVariable = "COTTON_FFMPEG_DIR";
        private const string CacheDirectoryName = "cotton-ffmpeg";

        private static readonly SemaphoreSlim DownloadGate = new(1, 1);
        private static string? _ffmpegPath;
        private static string? _ffprobePath;

        /// <summary>Returns the resolved ffmpeg executable path for the current OS.</summary>
        public static string GetFfmpegPath() =>
            _ffmpegPath ?? ResolveExistingExecutable(FfmpegPathEnvironmentVariable, GetExecutableName("ffmpeg")) ?? GetDownloadedExecutablePath("ffmpeg");

        /// <summary>Returns the resolved ffprobe executable path for the current OS.</summary>
        public static string GetFfprobePath() =>
            _ffprobePath ?? ResolveExistingExecutable(FfprobePathEnvironmentVariable, GetExecutableName("ffprobe")) ?? GetDownloadedExecutablePath("ffprobe");

        /// <summary>Ensures ffmpeg and ffprobe are available without writing to the application directory.</summary>
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

        /// <summary>Returns media duration in seconds, or null when ffprobe cannot determine it.</summary>
        public static async Task<double?> TryGetDurationSecondsAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            string arguments =
                "-v error -analyzeduration 100M -probesize 100M " +
                "-show_entries format=duration -of default=nw=1:nk=1 " +
                $"\"{url}\"";

            string? raw = await RunFfprobeAsync(arguments, timeout, cancellationToken).ConfigureAwait(false);
            return raw is null ? null : ParsePositiveDuration(raw.Trim());
        }

        /// <summary>Returns duration and primary audio/video codecs, or null when probing fails.</summary>
        public static async Task<MediaProbeInfo?> TryGetMediaProbeAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            string arguments =
                "-v error -analyzeduration 100M -probesize 100M " +
                "-of json -show_entries format=duration:stream=codec_name,codec_type " +
                $"\"{url}\"";

            string? raw = await RunFfprobeAsync(arguments, timeout, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(raw) ? null : ParseMediaProbe(raw);
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
            var startInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                ArgumentList = { "+x", path },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            if (process.Start())
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<string?> RunFfprobeAsync(
            string arguments,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFfprobePath(),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            bool completed = await WaitForProcessAsync(process, timeout, cancellationToken).ConfigureAwait(false);
            if (!completed || process.ExitCode != 0)
            {
                return null;
            }

            return await stdoutTask.ConfigureAwait(false);
        }

        private static async Task<bool> WaitForProcessAsync(
            Process process,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best effort cleanup after a timed-out ffprobe process.
                }

                return false;
            }
        }

        private static double? ParsePositiveDuration(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && value > 0
                    ? value
                    : null;
        }

        private static MediaProbeInfo? ParseMediaProbe(string raw)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(raw);
                JsonElement root = document.RootElement;

                return new MediaProbeInfo(
                    ParseProbeDuration(root),
                    ParseFirstStreamCodec(root, "video"),
                    ParseFirstStreamCodec(root, "audio"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static double? ParseProbeDuration(JsonElement root)
        {
            return root.TryGetProperty("format", out JsonElement format)
                && format.TryGetProperty("duration", out JsonElement durationElement)
                    ? ParsePositiveDuration(durationElement.GetString() ?? string.Empty)
                    : null;
        }

        private static string? ParseFirstStreamCodec(JsonElement root, string targetCodecType)
        {
            if (!root.TryGetProperty("streams", out JsonElement streams))
            {
                return null;
            }

            foreach (JsonElement stream in streams.EnumerateArray())
            {
                if (TryReadStreamCodec(stream, out string? codecType, out string? codecName)
                    && codecType == targetCodecType)
                {
                    return codecName;
                }
            }

            return null;
        }

        private static bool TryReadStreamCodec(
            JsonElement stream,
            out string? codecType,
            out string? codecName)
        {
            codecType = null;
            codecName = null;

            if (!stream.TryGetProperty("codec_type", out JsonElement typeElement)
                || !stream.TryGetProperty("codec_name", out JsonElement codecElement))
            {
                return false;
            }

            codecType = typeElement.GetString();
            codecName = codecElement.GetString();
            return codecName is not null;
        }
    }
}
