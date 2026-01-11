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

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            var filter =
                $"\"thumbnail,scale='min({size},iw)':'min({size},ih)':force_original_aspect_ratio=decrease\"";

            var args =
                "-hide_banner -loglevel error " +
                "-i pipe:0 " +
                $"-vf {filter} " +
                "-frames:v 1 " +
                "-an -sn -dn " +
                "-f webp pipe:1";

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFfmpegPath(),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start ffmpeg for video preview.");
            }

            var stdin = process.StandardInput.BaseStream;
            var stdout = process.StandardOutput.BaseStream;

            var outputMs = new MemoryStream();
            var copyInputTask = stream.CopyToAsync(stdin);
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            await copyInputTask;
            process.StandardInput.Close();

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync());

            if (process.ExitCode != 0)
            {
                var err = errorTask.Result;
                throw new InvalidOperationException($"ffmpeg video preview failed: {err}");
            }

            return outputMs.ToArray();
        }

        // ---------- FFmpeg presence ----------

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
