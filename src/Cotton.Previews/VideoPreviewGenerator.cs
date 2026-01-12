using System.Diagnostics;
using System.Net.Sockets;
using Cotton.Previews.Streams;
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

            var canSeek = stream.CanSeek;
            long? declaredLength = null;
            long? startPos = null;
            if (canSeek)
            {
                try
                {
                    declaredLength = stream.Length;
                    startPos = stream.Position;
                }
                catch
                {
                    canSeek = false;
                    declaredLength = null;
                    startPos = null;
                }
            }

            // Produce a high-quality 150x150 preview:
            // - pick a representative frame (via seeking at the container level by choosing window)
            // - scale with lanczos for sharpness
            // - pad to square so UI layout is consistent
            var filter = $"\"scale={size}:{size}:force_original_aspect_ratio=decrease:flags=lanczos,pad={size}:{size}:(ow-iw)/2:(oh-ih)/2\"";

            if (canSeek && declaredLength is not null && declaredLength.Value > 0)
            {
                long total = declaredLength.Value;
                long[] windows =
                [
                    4L * 1024 * 1024,
                    16L * 1024 * 1024,
                    64L * 1024 * 1024,
                    256L * 1024 * 1024,
                ];

                // 1) Prefer start-of-file window growth (best for faststart moov-at-begin)
                foreach (var window in windows)
                {
                    long len = Math.Min(window, total);
                    try
                    {
                        var w = new WindowedSeekStream(stream, start: 0, length: len);
                        return await RunFfmpegPipeAsync(w, filter, canSeek, declaredLength, startPos, windowOffset: 0, windowBytes: len)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // try next window
                    }
                }

                // 2) Probe end-of-file window growth (helps when moov is at the end)
                foreach (var window in windows)
                {
                    long len = Math.Min(window, total);
                    long start = Math.Max(0, total - len);
                    try
                    {
                        var w = new WindowedSeekStream(stream, start: start, length: len);
                        return await RunFfmpegPipeAsync(w, filter, canSeek, declaredLength, startPos, windowOffset: start, windowBytes: len)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // try next window
                    }
                }

                throw new InvalidOperationException(
                    $"ffmpeg video preview failed for all seek-window attempts. canSeek={canSeek}; length={declaredLength}; startPos={startPos}");
            }

            if (canSeek)
            {
                try { stream.Seek(0, SeekOrigin.Begin); } catch { }
            }

            return await RunFfmpegPipeAsync(stream, filter, canSeek, declaredLength, startPos, windowOffset: null, windowBytes: null)
                .ConfigureAwait(false);
        }

        private static async Task<byte[]> RunFfmpegPipeAsync(
            Stream input,
            string filter,
            bool canSeek,
            long? declaredLength,
            long? startPos,
            long? windowOffset,
            long? windowBytes)
        {
            var args =
                "-hide_banner -loglevel error " +
                "-err_detect ignore_err -fflags +discardcorrupt " +
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
            var copyInputTask = CopyToWithCountAsync(input, stdin, onWritten: n => bytesWritten += n);
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await copyInputTask.ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch (IOException ex) when (IsBrokenPipe(ex))
            {
                // ignore; ffmpeg may exit early
            }

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync()).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var err = await errorTask.ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"ffmpeg video preview failed. exitCode={process.ExitCode}; " +
                    $"bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}; startPos={startPos?.ToString() ?? "n/a"}; " +
                    $"stdinMode=pipe; windowOffset={windowOffset?.ToString() ?? "n/a"}; windowBytes={windowBytes?.ToString() ?? "n/a"}; stderr={err}");
            }

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"ffmpeg produced empty output. bytesWritten={bytesWritten}; canSeek={canSeek}; length={declaredLength?.ToString() ?? "n/a"}; windowOffset={windowOffset?.ToString() ?? "n/a"}; windowBytes={windowBytes?.ToString() ?? "n/a"}");
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
