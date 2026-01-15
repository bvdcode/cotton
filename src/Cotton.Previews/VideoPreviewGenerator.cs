using System.Diagnostics;
using Cotton.Previews.Http;
using Microsoft.Extensions.Logging;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public class VideoPreviewGenerator : IPreviewGenerator
    {
        private static ILogger? _logger;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

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
            var previewId = Guid.NewGuid().ToString("N")[..8];
            _logger?.LogInformation("[VideoPreview {PreviewId}] Starting generation, stream.CanSeek={CanSeek}, stream.Length={Length}", previewId, stream.CanSeek, stream.CanSeek ? stream.Length : -1);

            await CheckFfmpegAsync();

            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Video preview generation requires a seekable stream.");
            }

            try { stream.Seek(0, SeekOrigin.Begin); } catch { }

            byte[] pngFrame;
            _logger?.LogInformation("[VideoPreview {PreviewId}] Creating RangeStreamServer...", previewId);
            await using (var server = new RangeStreamServer(stream, _logger))
            {
                _logger?.LogInformation("[VideoPreview {PreviewId}] RangeStreamServer created, getting duration...", previewId);
                double? durationSeconds = await TryGetDurationSecondsAsync(previewId, server.Url).ConfigureAwait(false);
                _logger?.LogInformation("[VideoPreview {PreviewId}] Duration: {Duration}s", previewId, durationSeconds?.ToString() ?? "null");

                double seekSeconds = ComputeSeekSeconds(durationSeconds);
                _logger?.LogInformation("[VideoPreview {PreviewId}] Computed seek position: {SeekSeconds}s", previewId, seekSeconds);

                _logger?.LogInformation("[VideoPreview {PreviewId}] Extracting PNG frame...", previewId);
                pngFrame = await RunFfmpegHttpPngAsync(previewId, server.Url, seekSeconds).ConfigureAwait(false);
                _logger?.LogInformation("[VideoPreview {PreviewId}] PNG frame extracted, size={Size} bytes", previewId, pngFrame.Length);
            }

            _logger?.LogInformation("[VideoPreview {PreviewId}] RangeStreamServer disposed, passing to ImagePreviewGenerator...", previewId);
            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var pngStream = new MemoryStream(pngFrame);
            var result = await imagePreviewGenerator.GeneratePreviewWebPAsync(pngStream, size);
            _logger?.LogInformation("[VideoPreview {PreviewId}] Final WebP generated, size={Size} bytes", previewId, result.Length);
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

        private static async Task<double?> TryGetDurationSecondsAsync(string previewId, Uri url)
        {
            _logger?.LogInformation("[VideoPreview {PreviewId}] ffprobe starting for {Url}...", previewId, url);
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
                _logger?.LogWarning("[VideoPreview {PreviewId}] ffprobe failed to start", previewId);
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
                _logger?.LogWarning("[VideoPreview {PreviewId}] ffprobe timed out, killing process", previewId);
                try { process.Kill(true); } catch { }
                return null;
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                _logger?.LogWarning("[VideoPreview {PreviewId}] ffprobe exited with code {ExitCode}, stderr: {StdErr}", previewId, process.ExitCode, stderr);
                return null;
            }

            var s = (await stdoutTask.ConfigureAwait(false)).Trim();
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                _logger?.LogInformation("[VideoPreview {PreviewId}] ffprobe succeeded: duration={Duration}s", previewId, duration);
                return duration;
            }

            _logger?.LogWarning("[VideoPreview {PreviewId}] ffprobe output could not be parsed: {Output}", previewId, s);
            return null;
        }

        private static async Task<byte[]> RunFfmpegHttpPngAsync(string previewId, Uri url, double seekSeconds)
        {
            string ss = seekSeconds > 0 ? $"-ss {seekSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " : string.Empty;

            var args =
                "-hide_banner -loglevel error " +
                ss +
                $"-i \"{url}\" " +
                "-frames:v 1 " +
                "-an -sn -dn " +
                "-f image2pipe -vcodec png pipe:1";

            _logger?.LogInformation("[VideoPreview {PreviewId}] ffmpeg starting with args: {Args}", previewId, args);

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
                _logger?.LogError("[VideoPreview {PreviewId}] ffmpeg failed to start", previewId);
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
                _logger?.LogWarning("[VideoPreview {PreviewId}] ffmpeg timed out, killing process", previewId);
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException("ffmpeg video preview timed out.");
            }

            var stderr = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                _logger?.LogError("[VideoPreview {PreviewId}] ffmpeg exited with code {ExitCode}, stderr: {StdErr}", previewId, process.ExitCode, stderr);
                throw new InvalidOperationException($"ffmpeg video preview failed. exitCode={process.ExitCode}; stderr={stderr}");
            }

            if (outputMs.Length == 0)
            {
                _logger?.LogError("[VideoPreview {PreviewId}] ffmpeg produced no output", previewId);
                throw new InvalidOperationException("ffmpeg produced empty output.");
            }

            _logger?.LogInformation("[VideoPreview {PreviewId}] ffmpeg succeeded, output size={Size} bytes", previewId, outputMs.Length);
            return outputMs.ToArray();
        }

        private static async Task CheckFfmpegAsync()
        {
            if (!File.Exists(GetFfmpegPath()) || !File.Exists(GetFfprobePath()))
            {
                _logger?.LogInformation("Downloading ffmpeg/ffprobe...");
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    Process.Start("chmod", "+x ffmpeg");
                    Process.Start("chmod", "+x ffprobe");
                }
                _logger?.LogInformation("ffmpeg/ffprobe downloaded");
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
