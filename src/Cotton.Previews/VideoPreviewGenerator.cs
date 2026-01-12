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
                }
                catch
                {
                    canSeek = false;
                    declaredLength = null;
                    startPos = null;
                }
            }

            var filter = $"\"thumbnail,scale='min({size},iw)':'min({size},ih)':force_original_aspect_ratio=decrease\"";

            // Strategy:
            // - For seekable streams: try offsets with progressively larger read windows.
            //   This minimizes reads for very large videos and increases chance of finding a decodable segment.
            // - For non-seekable streams: fall back to piping the full stream.
            if (canSeek && declaredLength is not null && declaredLength.Value > 0)
            {
                long length = declaredLength.Value;

                long[] windows =
                [
                    16L * 1024 * 1024,
                    64L * 1024 * 1024,
                    256L * 1024 * 1024,
                ];

                double[] ratios = [0.0, 0.02, 0.05, 0.15, 0.30, 0.50];

                Exception? last = null;
                foreach (var windowBytes in windows)
                {
                    foreach (var ratio in ratios)
                    {
                        long offset = (long)(length * ratio);
                        offset = Math.Clamp(offset, 0, Math.Max(0, length - 1));

                        try
                        {
                            stream.Seek(offset, SeekOrigin.Begin);
                            var bounded = new BoundedReadStream(stream, maxBytes: windowBytes);

                            return await RunFfmpegPipeAsync(
                                input: bounded,
                                filter: filter,
                                canSeek: canSeek,
                                declaredLength: declaredLength,
                                startPos: startPos,
                                windowOffset: offset,
                                windowBytes: windowBytes).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            last = ex;
                        }
                    }
                }

                throw new InvalidOperationException(
                    $"ffmpeg video preview failed for all seek+window attempts. canSeek={canSeek}; length={declaredLength}; startPos={startPos}; lastError={last}",
                    last);
            }

            if (canSeek)
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }
                catch
                {
                    // ignore
                }
            }

            return await RunFfmpegPipeAsync(
                input: stream,
                filter: filter,
                canSeek: canSeek,
                declaredLength: declaredLength,
                startPos: startPos,
                windowOffset: null,
                windowBytes: null).ConfigureAwait(false);
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
                // ffmpeg may exit early and close stdin; ignore broken pipe and evaluate exit code/output.
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
