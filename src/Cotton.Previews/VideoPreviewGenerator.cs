using System.Diagnostics;
using System.Net.Sockets;
using Cotton.Previews.Http;

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

            // Extracting the very first visible frame often produces tiny/low-res thumbnails (e.g. 16x16),
            // especially for some formats/encodes. Seek a bit into the timeline and decode at a higher
            // intermediate size, then downscale sharply to the requested preview size.
            int intermediate = Math.Max(size * 4, 512);
            const int seekSeconds = 10;

            var filter =
                $"\"" +
                $"scale={intermediate}:{intermediate}:force_original_aspect_ratio=decrease:flags=lanczos," +
                $"scale={size}:{size}:force_original_aspect_ratio=decrease:flags=lanczos," +
                $"pad={size}:{size}:(ow-iw)/2:(oh-ih)/2" +
                $"\"";

            Exception? last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await using var server = new RangeStreamServer(stream);
                    return await RunFfmpegHttpAsync(server.Url, filter, seekSeconds).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("End of file", StringComparison.OrdinalIgnoreCase)
                                                     || ex.Message.Contains("prematurely", StringComparison.OrdinalIgnoreCase))
                {
                    last = ex;
                    try { stream.Seek(0, SeekOrigin.Begin); } catch { }
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1))).ConfigureAwait(false);
                }
            }

            throw last ?? new InvalidOperationException("ffmpeg video preview failed.");
        }

        private static async Task<byte[]> RunFfmpegHttpAsync(Uri url, string filter, int seekSeconds)
        {
            var args =
                "-hide_banner -loglevel error " +
                $"-ss {seekSeconds} " +
                $"-i \"{url}\" " +
                $"-vf {filter} " +
                "-frames:v 1 " +
                "-an -sn -dn " +
                "-f webp pipe:1";

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

        private static bool IsBrokenPipe(IOException ex)
        {
            return ex.InnerException is SocketException { ErrorCode: 32 };
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
