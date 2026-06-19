// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates audio previews from embedded cover art or waveform data.
    /// </summary>
    public class AudioPreviewGenerator : IPreviewGenerator
    {
        private const int CoverArtExtractionTimeoutSeconds = 15;
        private const int WaveformExtractionTimeoutSeconds = 120;
        private const int WaveformSampleRateHz = 400;

        /// <inheritdoc />
        public int Version => 4;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150)
        {
            await FfmpegBinary.EnsureAvailableAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Audio preview generation requires a seekable stream.");
            }

            try { stream.Seek(0, SeekOrigin.Begin); } catch { }

            byte[]? imageBytes = null;
            Exception? coverArtException = null;
            await using (var server = new RangeStreamServer(stream))
            {
                try
                {
                    imageBytes = await ExtractCoverArtAsync(server.Url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    coverArtException = ex;
                }

                if (imageBytes is null)
                {
                    try
                    {
                        return await GenerateWaveformPreviewWebPAsync(server.Url, size).ConfigureAwait(false);
                    }
                    catch (Exception waveformException)
                    {
                        throw new InvalidOperationException(
                            "Failed to generate audio preview from both cover art and waveform.",
                            new AggregateException(coverArtException!, waveformException));
                    }
                }
            }

            ImagePreviewGenerator imagePreviewGenerator = new();
            await using var imageStream = new MemoryStream(imageBytes);
            return await imagePreviewGenerator.GeneratePreviewWebPAsync(imageStream, size);
        }

        private static async Task<byte[]> GenerateWaveformPreviewWebPAsync(Uri url, int size)
        {
            short[] samples = await DecodePcm16MonoAsync(url).ConfigureAwait(false);
            int bars = Math.Clamp(size / 10, 8, 20);
            float[] amplitudes = BuildAmplitudes(samples, bars);

            using var image = new Image<Rgba32>(size, size, new Rgba32(0, 0, 0, 0));
            image.Mutate(ctx =>
            {
                float barGap = Math.Max(5f, size / 48f);
                float sidePadding = Math.Max(10f, barGap * 1.5f);
                float availableWidth = size - (sidePadding * 2f) - ((bars - 1) * barGap);
                float barWidth = Math.Max(1f, availableWidth / bars);
                float totalBarsWidth = (bars * barWidth) + ((bars - 1) * barGap);
                float left = (size - totalBarsWidth) / 2f;
                float centerY = size / 2f;
                float minBarHeight = Math.Max(4f, size * 0.06f);
                float maxBarHeight = size * 0.82f;
                var barBrush = Brushes.Solid(Color.FromPixel(new Rgba32(
                    PreviewColorPalette.AccentGreenRed,
                    PreviewColorPalette.AccentGreenGreen,
                    PreviewColorPalette.AccentGreenBlue)));

                ctx.Paint(canvas =>
                {
                    for (int i = 0; i < bars; i++)
                    {
                        float amplitude = amplitudes[i];
                        float barHeight = Math.Max(minBarHeight, amplitude * maxBarHeight);
                        float x = left + (i * (barWidth + barGap));
                        float y = centerY - (barHeight / 2f);
                        float radius = barWidth / 2f;

                        if (barHeight <= barWidth)
                        {
                            canvas.Fill(barBrush, new EllipsePolygon(x + radius, centerY, radius));
                            continue;
                        }

                        float bodyHeight = Math.Max(0, barHeight - barWidth);
                        if (bodyHeight > 0)
                        {
                            canvas.Fill(barBrush, new RectanglePolygon(x, y + radius, barWidth, bodyHeight));
                        }

                        canvas.Fill(barBrush, new EllipsePolygon(x + radius, y + radius, radius));
                        canvas.Fill(barBrush, new EllipsePolygon(x + radius, y + barHeight - radius, radius));
                    }
                });
            });

            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<short[]> DecodePcm16MonoAsync(Uri url)
        {
            var args =
                "-hide_banner -loglevel error -nostdin " +
                $"-i \"{url}\" " +
                "-vn -sn -dn " +
                $"-ac 1 -ar {WaveformSampleRateHz} " +
                "-f s16le pipe:1";

            var startInfo = new ProcessStartInfo
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start ffmpeg for waveform extraction.");
            }

            await using var outputMs = new MemoryStream();
            var copyOutputTask = process.StandardOutput.BaseStream.CopyToAsync(outputMs);
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaveformExtractionTimeoutSeconds));
            try
            {
                await Task.WhenAll(copyOutputTask, process.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException($"ffmpeg waveform extraction timed out after {WaveformExtractionTimeoutSeconds} seconds.");
            }

            var stderr = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg waveform extraction failed. exitCode={process.ExitCode}; stderr={stderr}");
            }

            byte[] bytes = outputMs.ToArray();
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException("No audio samples were produced for waveform preview.");
            }

            var samples = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            return samples;
        }

        private static float[] BuildAmplitudes(short[] samples, int bars)
        {
            var result = new float[bars];
            if (samples.Length == 0)
            {
                return result;
            }

            int samplesPerBar = Math.Max(1, samples.Length / bars);
            float max = 0;

            for (int i = 0; i < bars; i++)
            {
                int start = i * samplesPerBar;
                int end = Math.Min(samples.Length, start + samplesPerBar);

                double sumSquares = 0;
                int count = 0;

                for (int j = start; j < end; j++)
                {
                    float normalized = samples[j] / 32768f;
                    sumSquares += normalized * normalized;
                    count++;
                }

                var rms = count == 0 ? 0 : Math.Sqrt(sumSquares / count);
                var value = (float)Math.Pow(rms, 0.55);

                result[i] = value;
                max = Math.Max(max, value);
            }

            if (max > 0)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] /= max;
                }

                int edgeBars = Math.Max(2, bars / 10);
                for (int i = 0; i < edgeBars; i++)
                {
                    float taper = (i + 1f) / (edgeBars + 1f);
                    result[i] *= taper;
                    result[result.Length - 1 - i] *= taper;
                }
            }

            return result;
        }

        private static async Task<byte[]> ExtractCoverArtAsync(Uri url)
        {
            var args =
                "-hide_banner -loglevel error -nostdin " +
                $"-i \"{url}\" " +
                "-an -sn -dn " +
                "-frames:v 1 " +
                "-f image2pipe -vcodec png pipe:1";

            var startInfo = new ProcessStartInfo
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CoverArtExtractionTimeoutSeconds));
            try
            {
                await Task.WhenAll(copyOutputTask, process.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
            }
            catch
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException($"ffmpeg audio cover art extraction timed out after {CoverArtExtractionTimeoutSeconds} seconds.");
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
    }
}
