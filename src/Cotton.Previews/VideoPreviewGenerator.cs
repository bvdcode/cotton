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
                pngFrame = await RunFfmpegHttpPngAsync(server.Url).ConfigureAwait(false);
            }
            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var pngStream = new MemoryStream(pngFrame);
            return await imagePreviewGenerator.GeneratePreviewWebPAsync(pngStream);
        }

        private static async Task<byte[]> RunFfmpegHttpPngAsync(Uri url)
        {
            var args =
                "-hide_banner -loglevel error " +
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

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync()).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var err = await errorTask.ConfigureAwait(false);
                throw new InvalidOperationException($"ffmpeg video preview failed. exitCode={process.ExitCode}; stderr={err}");
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
