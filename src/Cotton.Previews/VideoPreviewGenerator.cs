// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using System.Diagnostics;

namespace Cotton.Previews
{
    public class VideoPreviewGenerator : IPreviewGenerator
    {
        public int Version => 2;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "video/mp4",
            "video/webm",
            "video/ogg",
            "video/avi",
            "video/mov",
            "video/quicktime",
            "video/x-quicktime",
            "video/mkv",
            "video/msvideo",
            "video/x-msvideo",
            "video/vnd.avi",
            "video/matroska",
            "video/x-matroska",
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150)
        {
            await FfmpegBinary.EnsureAvailableAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Video preview generation requires a seekable stream.");
            }

            stream.Seek(0, SeekOrigin.Begin);

            byte[] imageBytes;
            await using (var server = new RangeStreamServer(stream))
            {
                var coverArt = await TryExtractCoverArtAsync(server.Url).ConfigureAwait(false);
                if (coverArt is not null)
                {
                    imageBytes = coverArt;
                }
                else
                {
                    double? durationSeconds = await FfmpegBinary.TryGetDurationSecondsAsync(
                            server.Url,
                            timeout: TimeSpan.FromSeconds(15))
                        .ConfigureAwait(false);
                    double seekSeconds = ComputeSeekSeconds(durationSeconds);
                    imageBytes = await RunFfmpegHttpPngAsync(server.Url, seekSeconds).ConfigureAwait(false);
                }
            }

            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var imageStream = new MemoryStream(imageBytes);
            var result = await imagePreviewGenerator.GeneratePreviewWebPAsync(imageStream, size);
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

        private static async Task<byte[]?> TryExtractCoverArtAsync(Uri url)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cotton_cover_{Guid.NewGuid():N}");
            try
            {
                string args =
                    "-hide_banner -loglevel error " +
                    $"-dump_attachment:t:0 \"{tempFile}\" " +
                    $"-i \"{url}\" -y";

                ProcessStartInfo startInfo = new()
                {
                    FileName = FfmpegBinary.GetFfmpegPath(),
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = startInfo };
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Failed to start ffmpeg for video cover art extraction.");
                }

                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                bool completed = await FfmpegBinary.WaitForProcessAsync(
                    process,
                    TimeSpan.FromSeconds(15),
                    CancellationToken.None).ConfigureAwait(false);
                await stderrTask.ConfigureAwait(false);
                if (!completed)
                {
                    return null;
                }

                if (File.Exists(tempFile))
                {
                    byte[] data = await File.ReadAllBytesAsync(tempFile).ConfigureAwait(false);
                    if (data.Length > 0)
                    {
                        return data;
                    }
                }

                return null;
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private static async Task<byte[]> RunFfmpegHttpPngAsync(Uri url, double seekSeconds)
        {
            string ss = seekSeconds > 0 ? $"-ss {seekSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " : string.Empty;

            string args =
                "-hide_banner -loglevel error " +
                ss +
                $"-i \"{url}\" " +
                "-frames:v 1 " +
                "-an -sn -dn " +
                "-f image2pipe -vcodec png pipe:1";

            await using MemoryStream outputMs = new();
            await FfmpegProcessRunner.RunAsync(
                args,
                outputMs,
                TimeSpan.FromSeconds(30),
                "video preview").ConfigureAwait(false);

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException("ffmpeg produced empty output.");
            }

            return outputMs.ToArray();
        }
    }
}
