// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using Mediana.Drawing;
using SixLabors.ImageSharp;
using ImagePixel = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    public class AudioPreviewGenerator : IPreviewGenerator
    {
        public string Id => "audio";

        private const int CoverArtExtractionTimeoutSeconds = 15;
        private const int WaveformExtractionTimeoutSeconds = 120;
        private const int WaveformSampleRateHz = 400;

        public int Version => 5;

        public int Priority => 0;

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
            await FfmpegBinary.EnsureAvailableAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Audio preview generation requires a seekable stream.");
            }

            stream.Seek(0, SeekOrigin.Begin);

            await using RangeStreamServer server = new RangeStreamServer(stream);
            return await GeneratePreviewWebPAsync(server.Url, size).ConfigureAwait(false);
        }

        internal async Task<byte[]> GeneratePreviewWebPAsync(Uri url, int size)
        {
            byte[]? imageBytes = null;
            Exception? coverArtException = null;
            try
            {
                imageBytes = await ExtractCoverArtAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                coverArtException = ex;
            }

            if (imageBytes is null)
            {
                try
                {
                    return await GenerateWaveformPreviewWebPAsync(url, size).ConfigureAwait(false);
                }
                catch (Exception waveformException)
                {
                    throw new InvalidOperationException(
                        "Failed to generate audio preview from both cover art and waveform.",
                        new AggregateException(coverArtException!, waveformException));
                }
            }

            ImagePreviewGenerator imagePreviewGenerator = new();
            await using MemoryStream imageStream = new MemoryStream(imageBytes);
            return await imagePreviewGenerator.GeneratePreviewWebPAsync(imageStream, size);
        }

        private static async Task<byte[]> GenerateWaveformPreviewWebPAsync(Uri url, int size)
        {
            short[] samples = await DecodePcm16MonoAsync(url).ConfigureAwait(false);
            int bars = Math.Clamp(size / 10, 8, 20);
            float[] amplitudes = BuildAmplitudes(samples, bars);

            using PixelBuffer pixels = new(size, size);
            pixels.Clear(new Rgba32(0, 0, 0, 0));
            WaveformPreviewRenderer.Draw(pixels, amplitudes);
            using Image<ImagePixel> image = Image.WrapMemory<ImagePixel>(pixels.Pixels, size, size);

            await using MemoryStream output = new MemoryStream();
            await image.SaveAsWebpAsync(output, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<short[]> DecodePcm16MonoAsync(Uri url)
        {
            string args =
                "-hide_banner -loglevel error -nostdin " +
                $"-i \"{url}\" " +
                "-vn -sn -dn " +
                $"-ac 1 -ar {WaveformSampleRateHz} " +
                "-f s16le pipe:1";

            await using MemoryStream outputMs = new();
            await FfmpegProcessRunner.RunAsync(
                args,
                outputMs,
                TimeSpan.FromSeconds(WaveformExtractionTimeoutSeconds),
                "waveform extraction").ConfigureAwait(false);

            byte[] bytes = outputMs.ToArray();
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException("No audio samples were produced for waveform preview.");
            }

            short[] samples = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            return samples;
        }

        private static float[] BuildAmplitudes(short[] samples, int bars)
        {
            float[] result = new float[bars];
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

                double rms = count == 0 ? 0 : Math.Sqrt(sumSquares / count);
                float value = (float)Math.Pow(rms, 0.55);

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
            string args =
                "-hide_banner -loglevel error -nostdin " +
                $"-i \"{url}\" " +
                "-an -sn -dn " +
                "-frames:v 1 " +
                "-f image2pipe -vcodec png pipe:1";

            await using MemoryStream outputMs = new();
            await FfmpegProcessRunner.RunAsync(
                args,
                outputMs,
                TimeSpan.FromSeconds(CoverArtExtractionTimeoutSeconds),
                "audio cover art extraction").ConfigureAwait(false);

            if (outputMs.Length == 0)
            {
                throw new InvalidOperationException("No cover art found in audio file.");
            }

            return outputMs.ToArray();
        }
    }
}
