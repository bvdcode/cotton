using System.Diagnostics;
using System.Globalization;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public class VideoPreviewGenerator : IPreviewGenerator
    {
        // Files up to this size are considered "small" and processed in accurate mode
        private const long SmallFileMaxBytes = 256L * 1024 * 1024;   // 256 MB

        // For any file we will read at most this many bytes
        private const long MaxBytesToRead = 256L * 1024 * 1024;      // 256 MB

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

            long length = stream.Length;

            // Small file: use accurate mode by inspecting the whole stream
            if (length is <= SmallFileMaxBytes)
            {
                return await GenerateAccurateFromWholeStreamAsync(stream, size);
            }

            // Large file: use fast mode reading only the front of the stream
            return await GenerateFastFromFrontOfStreamAsync(stream, size);
        }

        // ---------- FFmpeg/FFprobe presence ----------

        private static async Task CheckFfmpegAsync()
        {
            if (!File.Exists(GetFfmpegPath()) || !File.Exists(GetFfprobePath()))
            {
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // Make binaries executable on Unix
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

        // ---------- Mode 1: small file, accurate extraction ----------

        private static async Task<byte[]> GenerateAccurateFromWholeStreamAsync(Stream stream, int size)
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            var workDir = Path.Combine(Path.GetTempPath(), "cotton-previews-video");
            Directory.CreateDirectory(workDir);

            var inputPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.input");
            var outputPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.webp");

            try
            {
                // Dump the entire stream to a temporary file (file is guaranteed to be reasonably sized)
                await using (var fs = new FileStream(
                    inputPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await stream.CopyToAsync(fs);
                }

                // Probe duration using ffprobe
                var durationSeconds = await TryGetDurationSecondsAsync(inputPath);

                double midSeconds;
                if (durationSeconds.HasValue && durationSeconds.Value > 0.5)
                {
                    // Choose middle timestamp with a small safety margin from the end
                    midSeconds = durationSeconds.Value / 2.0;
                    midSeconds = Math.Clamp(midSeconds, 0.0, Math.Max(0.0, durationSeconds.Value - 0.5));
                }
                else
                {
                    // Fallback to 5 seconds if duration unknown
                    midSeconds = 5.0;
                }

                var timeStr = midSeconds.ToString("0.###", CultureInfo.InvariantCulture);

                var filter =
                    $"\"scale='min({size},iw)':'min({size},ih)':force_original_aspect_ratio=decrease\"";

                var args =
                    "-hide_banner -loglevel error " +
                    $"-ss {timeStr} " +
                    $"-i \"{inputPath}\" " +
                    "-frames:v 1 " +
                    $"-vf {filter} " +
                    "-an -sn -dn " +
                    $"-y \"{outputPath}\"";

                await RunProcessAsync(GetFfmpegPath(), args);

                return await File.ReadAllBytesAsync(outputPath);
            }
            finally
            {
                SafeDelete(inputPath);
                SafeDelete(outputPath);
            }
        }

        private static async Task<double?> TryGetDurationSecondsAsync(string inputPath)
        {
            var args =
                "-v error " +
                "-show_entries format=duration " +
                "-of default=noprint_wrappers=1:nokey=1 " +
                $"\"{inputPath}\"";

            var (exitCode, stdout, _) = await RunProcessCaptureAsync(GetFfprobePath(), args);

            if (exitCode != 0)
            {
                // stderr can be logged if needed
                return null;
            }

            var s = stdout.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur) && dur > 0)
                return dur;

            return null;
        }

        // ---------- Mode 2: large file, fast front-only extraction ----------

        private static async Task<byte[]> GenerateFastFromFrontOfStreamAsync(Stream source, int size)
        {
            if (source.CanSeek)
                source.Seek(0, SeekOrigin.Begin);

            // Use ffmpeg thumbnail filter to pick a representative frame from the front of the stream
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
                throw new InvalidOperationException("Failed to start ffmpeg for fast video preview.");
            }

            var stdin = process.StandardInput.BaseStream;
            var stdout = process.StandardOutput.BaseStream;

            var outputMs = new MemoryStream();

            // Run feeding and reading in parallel: write limited bytes into ffmpeg stdin and read webp from stdout
            var copyInputTask = CopyLimitedAsync(source, stdin, MaxBytesToRead);
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            await copyInputTask;
            // Signal ffmpeg that there is no more input
            process.StandardInput.Close();

            await Task.WhenAll(copyOutputTask, errorTask, process.WaitForExitAsync());

            if (process.ExitCode != 0)
            {
                var err = errorTask.Result;
                throw new InvalidOperationException($"ffmpeg fast preview failed: {err}");
            }

            return outputMs.ToArray();
        }

        private static async Task CopyLimitedAsync(Stream src, Stream dst, long maxBytes)
        {
            var buffer = new byte[64 * 1024];
            long total = 0;

            while (total < maxBytes)
            {
                var toRead = (int)Math.Min(buffer.Length, maxBytes - total);
                var read = await src.ReadAsync(buffer.AsMemory(0, toRead));
                if (read <= 0)
                {
                    break;
                }

                await dst.WriteAsync(buffer.AsMemory(0, read));
                total += read;
            }

            await dst.FlushAsync();
        }

        // ---------- Helper methods for processes and files ----------

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignore failures
            }
        }

        private static async Task RunProcessAsync(string fileName, string arguments)
        {
            var (exitCode, _, stderr) = await RunProcessCaptureAsync(fileName, arguments);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"{fileName} failed: {stderr}");
            }
        }

        private static async Task<(int exitCode, string stdout, string stderr)> RunProcessCaptureAsync(
            string fileName,
            string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {fileName}");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());

            return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
        }
    }
}
