// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana;
using Mediana.Core.Audio;
using Mediana.Core.Memory;
using Mediana.Drawing;
using Mediana.Processing.Audio;
using SixLabors.ImageSharp;
using ImagePixel = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    internal static class AudioWaveformPreview
    {
        private const int WaveformSampleRateHz = 400;
        private const int SamplesPerWindow = 40;

        public static async Task<byte[]> RenderAsync(AudioSession session, MediaBufferAllocator allocator, int size,
            CancellationToken cancellationToken)
        {
            List<AudioLevel> levels = await ReadLevelsAsync(session, allocator, cancellationToken).ConfigureAwait(false);
            float[] amplitudes = BuildAmplitudes(levels, Math.Clamp(size / 10, 8, 20));
            using PixelBuffer pixels = new(size, size);
            pixels.Clear(new Rgba32(0, 0, 0, 0));
            WaveformPreviewRenderer.Draw(pixels, amplitudes);
            using Image<ImagePixel> image = Image.WrapMemory<ImagePixel>(pixels.Pixels, size, size);
            using MemoryStream output = new();
            await PreviewImageEncoder.SaveAsync(image, output, size, cancellationToken).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<List<AudioLevel>> ReadLevelsAsync(AudioSession session, MediaBufferAllocator allocator,
            CancellationToken cancellationToken)
        {
            AudioFrame? first = await session.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (first is null)
            {
                throw new InvalidDataException("No audio samples were produced for waveform preview.");
            }

            try
            {
                AudioChannelMixer mono = AudioDownmix.CreateMono(first.Format);
                AudioWaveformOptions options = new(WaveformSampleRateHz, SamplesPerWindow)
                {
                    SampleFormat = AudioSampleFormat.Signed16,
                    Resampling = new AudioResampleOptions { CutoffRatio = 0.97, BoundaryMode = AudioBoundaryMode.Reflect },
                };
                await using AudioWaveformReader waveform = new(ReadFrameAsync, mono, options, allocator);
                List<AudioLevel> levels = [];
                while (await waveform.ReadLevelAsync(cancellationToken).ConfigureAwait(false) is AudioLevel level)
                {
                    levels.Add(level);
                }
                return levels;
            }
            finally
            {
                first?.Dispose();
            }

            ValueTask<AudioFrame?> ReadFrameAsync(CancellationToken token)
            {
                AudioFrame? pending = first;
                first = null;
                return pending is null ? session.ReadFrameAsync(token) : ValueTask.FromResult<AudioFrame?>(pending);
            }
        }

        private static float[] BuildAmplitudes(List<AudioLevel> levels, int bars)
        {
            if (levels.Count == 0)
            {
                throw new InvalidDataException("No audio levels were produced for waveform preview.");
            }

            long samples = levels[^1].StartSample + levels[^1].SampleCount;
            float[] result = new float[bars];
            int nextLevel = 0;

            for (int bar = 0; bar < bars; bar++)
            {
                double start = bar * (double)samples / bars;
                double end = (bar + 1) * (double)samples / bars;
                double squares = 0;
                for (int index = nextLevel; index < levels.Count; index++)
                {
                    AudioLevel level = levels[index];
                    double levelEnd = level.StartSample + level.SampleCount;
                    double overlap = Math.Min(end, levelEnd) - Math.Max(start, level.StartSample);
                    squares += level.RootMeanSquare * level.RootMeanSquare * overlap;
                    if (levelEnd > end)
                    {
                        break;
                    }
                    nextLevel = index + 1;
                }
                result[bar] = (float)Math.Pow(squares / (end - start), 0.275);
            }

            float maximum = result.Max();
            if (maximum > 0)
            {
                int edgeBars = Math.Max(2, bars / 10);
                for (int bar = 0; bar < bars; bar++)
                {
                    int edgeDistance = Math.Min(bar + 1, bars - bar);
                    float taper = Math.Min(1, (float)edgeDistance / (edgeBars + 1));
                    result[bar] = result[bar] / maximum * taper;
                }
            }

            return result;
        }
    }
}
