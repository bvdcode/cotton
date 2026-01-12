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

            // Snapshot for diagnostics.
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
                    canSeek = false;
                    declaredLength = null;
                    startPos = null;
                }
            }

            var filter =
                $"\"thumbnail,scale='min({size},iw)':'min({size},ih)':force_original_aspect_ratio=decrease\"";

            // For MP4/MOV it is common that piping via stdin still breaks demuxing in some cases.
            // Use a temp file input (still preserves the original stream abstraction) to let ffmpeg seek.
            await using var tempInput = await TrySpoolToTempFileAsync(stream, canSeek, declaredLength).ConfigureAwait(false);

            string args;
            if (tempInput != null)
            {
                args =
                    "-hide_banner -loglevel error " +
                    "-err_detect ignore_err -fflags +discardcorrupt " +
                    $"-i \"{tempInput.Name}\" " +
                    $"-vf {filter} " +
                    "-frames:v 1 " +
                    "-an -sn -dn " +
                    "-f webp pipe:1";
            }
            else
            {
                args =
                    "-hide_banner -loglevel error " +
                    "-err_detect ignore_err -fflags +discardcorrupt " +
                    "-i pipe:0 " +
                    $"-vf {filter} " +
                    "-frames:v 1 " +
                    "-an -sn -dn " +
                    "-f webp pipe:1";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = GetFfmpegPath(),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = tempInput == null,
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

            Task copyInputTask;
            long bytesWritten = 0;
            if (tempInput == null)
            {
                await using var stdin = process.StandardInput.BaseStream;
                copyInputTask = CopyToWithCountAsync(stream, stdin, onWritten: n => bytesWritten += n);
            }
            else
            {
                copyInputTask = Task.CompletedTask;
                bytesWritten = declaredLength ?? 0;
            }

            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            await copyInputTask.ConfigureAwait(false);
            if (tempInput == null)
            {
                process.StandardInput.Close();
            }

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync()).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var err = await errorTask.ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"ffmpeg video preview failed. exitCode={process.ExitCode}; " +
                    $"bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}; startPos={startPos?.ToString() ?? "n/a"}; " +
                    $"stdinMode={(tempInput == null ? "pipe" : "file")}; stderr={err}");
            }

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"ffmpeg produced empty output. bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}; stdinMode={(tempInput == null ? "pipe" : "file")}");
            }

            return outputMs.ToArray();
        }

        private static async Task<FileStream?> TrySpoolToTempFileAsync(Stream stream, bool canSeek, long? declaredLength)
        {
            // Only spool when we can reliably rewind and the file is not absurdly large.
            if (!canSeek)
            {
                return null;
            }

            // Avoid unexpected disk usage for extremely large videos; keep stdin path available.
            const long maxSpoolBytes = 512L * 1024 * 1024;
            if (declaredLength is not null && declaredLength.Value > maxSpoolBytes)
            {
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"cotton-video-preview-{Guid.NewGuid():N}.mp4");
            var writeOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                BufferSize = 1024 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
            };

            await using (var tmpWrite = new FileStream(tempPath, writeOptions))
            {
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(tmpWrite).ConfigureAwait(false);
                await tmpWrite.FlushAsync().ConfigureAwait(false);
            }

            // Open for read; DeleteOnClose cleans it up when disposed.
            var readOptions = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 1024 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
            };

            return new FileStream(tempPath, readOptions);
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
