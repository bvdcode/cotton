// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

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
            string arguments =
                "-v error -analyzeduration 100M -probesize 100M " +
                "-show_entries format=duration -of default=nw=1:nk=1 " +
                $"\"{url}\"";

            string? raw = await RunFfprobeAsync(arguments, timeout, cancellationToken).ConfigureAwait(false);
            return raw is null ? null : ParsePositiveDuration(raw.Trim());
        }

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
            catch
            {
                TryKillProcess(process);
                return false;
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private static MediaProbeInfo? ParseMediaProbe(string raw)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(raw);
                double? duration = TryReadDuration(doc.RootElement);
                var (videoCodec, audioCodec) = TryReadStreamCodecs(doc.RootElement);

                return duration is null && videoCodec is null && audioCodec is null
                    ? null
                    : new MediaProbeInfo(duration, videoCodec, audioCodec);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static double? TryReadDuration(JsonElement root)
        {
            if (!root.TryGetProperty("format", out var format)
                || !format.TryGetProperty("duration", out var durationElement)
                || durationElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return ParsePositiveDuration(durationElement.GetString());
        }

        private static double? ParsePositiveDuration(string? raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                && seconds > 0
                    ? seconds
                    : null;
        }

        private static (string? VideoCodec, string? AudioCodec) TryReadStreamCodecs(JsonElement root)
        {
            if (!root.TryGetProperty("streams", out var streams)
                || streams.ValueKind != JsonValueKind.Array)
            {
                return (null, null);
            }

            string? videoCodec = null;
            string? audioCodec = null;
            foreach (var stream in streams.EnumerateArray())
            {
                AssignCodec(stream, ref videoCodec, ref audioCodec);
            }

            return (videoCodec, audioCodec);
        }

        private static void AssignCodec(JsonElement stream, ref string? videoCodec, ref string? audioCodec)
        {
            if (!TryReadCodec(stream, out string? type, out string? codec))
            {
                return;
            }

            if (type == "video" && videoCodec is null)
            {
                videoCodec = codec;
            }
            else if (type == "audio" && audioCodec is null)
            {
                audioCodec = codec;
            }
        }

        private static bool TryReadCodec(JsonElement stream, out string? type, out string? codec)
        {
            type = null;
            codec = null;

            if (!stream.TryGetProperty("codec_type", out var typeElement)
                || !stream.TryGetProperty("codec_name", out var codecElement))
            {
                return false;
            }

            codec = codecElement.GetString();
            if (string.IsNullOrWhiteSpace(codec))
            {
                return false;
            }

            type = typeElement.GetString();
            return true;
        }
    }
}
