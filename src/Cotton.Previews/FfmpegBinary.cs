// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public sealed record MediaProbeInfo(double? DurationSeconds, string? VideoCodec, string? AudioCodec);

    public static class FfmpegBinary
    {
        private static readonly SemaphoreSlim DownloadGate = new(1, 1);

        public static string GetFfmpegPath() =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffmpeg.exe" : "./ffmpeg";

        public static string GetFfprobePath() =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffprobe.exe" : "./ffprobe";

        public static async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
        {
            if (File.Exists(GetFfmpegPath()) && File.Exists(GetFfprobePath()))
            {
                return;
            }

            await DownloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(GetFfmpegPath()) && File.Exists(GetFfprobePath()))
                {
                    return;
                }

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).ConfigureAwait(false);

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    using var chmodFfmpeg = Process.Start("chmod", "+x ffmpeg");
                    using var chmodFfprobe = Process.Start("chmod", "+x ffprobe");
                    if (chmodFfmpeg is not null)
                    {
                        await chmodFfmpeg.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    if (chmodFfprobe is not null)
                    {
                        await chmodFfprobe.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
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
            await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);

            string arguments =
                "-v error -analyzeduration 100M -probesize 100M " +
                "-show_entries format=duration -of default=nw=1:nk=1 " +
                $"\"{url}\"";

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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            string raw = (await stdoutTask.ConfigureAwait(false)).Trim();
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                && seconds > 0)
            {
                return seconds;
            }

            return null;
        }

        public static async Task<MediaProbeInfo?> TryGetMediaProbeAsync(
            Uri url,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);

            string arguments =
                "-v error -analyzeduration 100M -probesize 100M " +
                "-of json -show_entries format=duration:stream=codec_name,codec_type " +
                $"\"{url}\"";

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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            string raw = await stdoutTask.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return ParseMediaProbe(raw);
        }

        private static MediaProbeInfo? ParseMediaProbe(string raw)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(raw);
                double? duration = null;
                if (doc.RootElement.TryGetProperty("format", out var format)
                    && format.TryGetProperty("duration", out var durationElement)
                    && durationElement.ValueKind == JsonValueKind.String
                    && double.TryParse(
                        durationElement.GetString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double seconds)
                    && seconds > 0)
                {
                    duration = seconds;
                }

                string? videoCodec = null;
                string? audioCodec = null;
                if (doc.RootElement.TryGetProperty("streams", out var streams)
                    && streams.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stream in streams.EnumerateArray())
                    {
                        if (!stream.TryGetProperty("codec_type", out var typeElement)
                            || !stream.TryGetProperty("codec_name", out var codecElement))
                        {
                            continue;
                        }

                        string? codec = codecElement.GetString();
                        if (string.IsNullOrWhiteSpace(codec))
                        {
                            continue;
                        }

                        string? type = typeElement.GetString();
                        if (type == "video" && videoCodec is null)
                        {
                            videoCodec = codec;
                        }
                        else if (type == "audio" && audioCodec is null)
                        {
                            audioCodec = codec;
                        }
                    }
                }

                return duration is null && videoCodec is null && audioCodec is null
                    ? null
                    : new MediaProbeInfo(duration, videoCodec, audioCodec);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
