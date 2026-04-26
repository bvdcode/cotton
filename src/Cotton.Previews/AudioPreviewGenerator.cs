using Cotton.Previews.Http;
using System.Diagnostics;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public class AudioPreviewGenerator : IPreviewGenerator
    {
        public int Version => 0;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "audio/mpeg",
            "audio/mp3",
            "audio/flac",
            "audio/ogg",
            "audio/wav",
            "audio/x-wav",
            "audio/aac",
            "audio/mp4",
            "audio/x-m4a",
            "audio/x-flac",
            "audio/opus",
            "audio/webm",
            "audio/x-aiff",
            "audio/aiff",
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150)
        {
            await CheckFfmpegAsync();
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Audio preview generation requires a seekable stream.");
            }

            try { stream.Seek(0, SeekOrigin.Begin); } catch { }

            byte[] imageBytes;
            await using (var server = new RangeStreamServer(stream))
            {
                imageBytes = await ExtractCoverArtAsync(server.Url).ConfigureAwait(false);
            }

            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var imageStream = new MemoryStream(imageBytes);
            return await imagePreviewGenerator.GeneratePreviewWebPAsync(imageStream, size);
        }

        private static async Task<byte[]> ExtractCoverArtAsync(Uri url)
        {
            var args =
                "-hide_banner -loglevel error " +
                $"-i \"{url}\" " +
                "-an -sn -dn " +
                "-frames:v 1 " +
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
                throw new InvalidOperationException("Failed to start ffmpeg for audio cover art extraction.");
            }

            await using var stdout = process.StandardOutput.BaseStream;
            await using var outputMs = new MemoryStream();
            var copyOutputTask = stdout.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await Task.WhenAll(copyOutputTask, process.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException("ffmpeg audio cover art extraction timed out.");
            }

            var stderr = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg audio cover art extraction failed. exitCode={process.ExitCode}; stderr={stderr}");
            }

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException("No cover art found in audio file.");
            }

            return outputMs.ToArray();
        }

        private static async Task CheckFfmpegAsync()
        {
            if (!File.Exists(GetFfmpegPath()))
            {
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    Process.Start("chmod", "+x ffmpeg");
                }
            }
        }

        private static string GetFfmpegPath()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffmpeg.exe" : "./ffmpeg";
        }
    }
}
