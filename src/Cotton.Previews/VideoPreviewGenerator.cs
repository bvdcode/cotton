using Cotton.Previews.Http;
using System.Diagnostics;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public class VideoPreviewGenerator : IPreviewGenerator
    {
        public IEnumerable<string> SupportedContentTypes =>
        [
            "video/mp4",
            "video/webm",
            "video/ogg",
            "video/avi",
            "video/mov",
            "video/mkv",
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150)
        {
            await CheckFfmpegAsync();
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Video preview generation requires a seekable stream.");
            }

            try { stream.Seek(0, SeekOrigin.Begin); } catch { }

            byte[] pngFrame;
            await using (var server = new RangeStreamServer(stream))
            {
                double? durationSeconds = await TryGetDurationSecondsAsync(server.Url).ConfigureAwait(false);
                double seekSeconds = ComputeSeekSeconds(durationSeconds);
                pngFrame = await RunFfmpegHttpPngAsync(server.Url, seekSeconds).ConfigureAwait(false);
            }

            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var pngStream = new MemoryStream(pngFrame);
            var result = await imagePreviewGenerator.GeneratePreviewWebPAsync(pngStream, size);
            return result;
        }

        private static double ComputeSeekSeconds(double? durationSeconds)
        {
            if (durationSeconds is null || durationSeconds <= 0)
            {
                return 0;
            }

            double t = durationSeconds.Value * 0.5;
            t = Math.Clamp(t, 0.5, Math.Max(0.5, durationSeconds.Value - 0.5));
            return t;
        }

        private static async Task<double?> TryGetDurationSecondsAsync(Uri url)
        {
            var args = $"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{url}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFfprobePath(),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(true); } catch { }
                return null;
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                return null;
            }

            var s = (await stdoutTask.ConfigureAwait(false)).Trim();
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                return duration;
            }

            return null;
        }

        private static async Task<byte[]> RunFfmpegHttpPngAsync(Uri url, double seekSeconds)
        {
            string ss = seekSeconds > 0 ? $"-ss {seekSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " : string.Empty;

            var args =
                "-hide_banner -loglevel error " +
                ss +
                $"-i \"{url}\" " +
                "-frames:v 1 " +
                "-an -sn -dn " +
                "-f image2pipe -vcodec png pipe:1";

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFfmpegPath(),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start ffmpeg for video preview.");
            }

            await using var stdout = process.StandardOutput.BaseStream;
            await using var outputMs = new MemoryStream();
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await Task.WhenAll(copyOutputTask, process.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException("ffmpeg video preview timed out.");
            }

            var stderr = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg video preview failed. exitCode={process.ExitCode}; stderr={stderr}");
            }

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException("ffmpeg produced empty output.");
            }

            return outputMs.ToArray();
        }

        private static async Task CheckFfmpegAsync()
        {
            if (!File.Exists(GetFfmpegPath()) || !File.Exists(GetFfprobePath()))
            {
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    Process.Start("chmod", "+x ffmpeg");
                    Process.Start("chmod", "+x ffprobe");
                }
            }
        }

        private static string GetFfmpegPath()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffmpeg.exe" : "./ffmpeg";
        }

        private static string GetFfprobePath()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffprobe.exe" : "./ffprobe";
        }
    }
}
