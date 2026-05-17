// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
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
    }
}
