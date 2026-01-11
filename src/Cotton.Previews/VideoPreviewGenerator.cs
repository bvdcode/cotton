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

            // Snapshot stream capabilities for diagnostics.
            var canSeek = stream.CanSeek;
            long? declaredLength = null;
            long? startPos = null;
            if (canSeek)
            {
                try
                {
                    declaredLength = stream.Length;
                    startPos = stream.Position;
                    stream.Seek(0, SeekOrigin.Begin);
                }
                catch
                {
                    // Ignore: some streams lie about CanSeek or throw.
                    canSeek = false;
                    declaredLength = null;
                    startPos = null;
                }
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

            await using var stdin = process.StandardInput.BaseStream;
            await using var stdout = process.StandardOutput.BaseStream;

            await using var outputMs = new MemoryStream();

            long bytesWritten = 0;
            var copyInputTask = CopyToWithCountAsync(stream, stdin, onWritten: n => bytesWritten += n);
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            await copyInputTask.ConfigureAwait(false);
            process.StandardInput.Close();

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync()).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var err = await errorTask.ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"ffmpeg video preview failed. exitCode={process.ExitCode}; " +
                    $"bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}; startPos={startPos?.ToString() ?? "n/a"}; " +
                    $"stderr={err}");
            }

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"ffmpeg produced empty output. bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}");
            }

            return outputMs.ToArray();
        }

        private static async Task CopyToWithCountAsync(Stream input, Stream output, Action<long> onWritten, int bufferSize = 1024 * 1024)
        {
            var buffer = new byte[bufferSize];
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                onWritten(read);
            }

            await output.FlushAsync().ConfigureAwait(false);
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
