// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace Cotton.Previews.Tests;

public class AudioPreviewGeneratorTests
{
    [Test]
    [Explicit("Requires ffmpeg binary availability or download.")]
    public async Task GeneratePreviewWebPAsync_WavWithoutCover_FallsBackToWaveform_AndWritesArtifact()
    {
        var generator = new AudioPreviewGenerator();
        byte[] wavBytes = CreatePcm16MonoWavBytes(sampleRate: 8000, durationSeconds: 2);
        using var stream = new MemoryStream(wavBytes);

        byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);
        Assert.That(Math.Max(image.Width, image.Height), Is.LessThanOrEqualTo(200));

        string artifactsDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        string artifactPath = Path.Combine(artifactsDirectory, "audio-waveform-preview.webp");
        await File.WriteAllBytesAsync(artifactPath, preview);

        TestContext.Progress.WriteLine($"Audio waveform preview artifact: {artifactPath}");
    }

    [Test]
    [Explicit("Requires ffmpeg binary availability or download.")]
    public async Task GeneratePreviewWebPAsync_WavWithoutCover_SavesDebugFrames_ForVisualInspection()
    {
        var generator = new AudioPreviewGenerator();
        string artifactsDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        (string Name, int LowFreq, int HighFreq, double EnvelopeHz)[] cases =
        [
            ("case-a", 180, 540, 0.55),
            ("case-b", 220, 660, 0.70),
            ("case-c", 300, 900, 0.95),
        ];

        foreach (var @case in cases)
        {
            byte[] wavBytes = CreatePcm16MonoWavBytes(
                sampleRate: 8000,
                durationSeconds: 2,
                lowFreq: @case.LowFreq,
                highFreq: @case.HighFreq,
                envelopeHz: @case.EnvelopeHz);

            using var stream = new MemoryStream(wavBytes);
            byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            string artifactPath = Path.Combine(artifactsDirectory, $"audio-waveform-preview-{@case.Name}.webp");
            await File.WriteAllBytesAsync(artifactPath, preview);
            TestContext.Progress.WriteLine($"Audio waveform preview debug artifact: {artifactPath}");
        }
    }

    private static byte[] CreatePcm16MonoWavBytes(
        int sampleRate,
        int durationSeconds,
        double lowFreq = 220,
        double highFreq = 660,
        double envelopeHz = 0.7)
    {
        int totalSamples = sampleRate * durationSeconds;
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = 0.25 + 0.75 * Math.Abs(Math.Sin(2 * Math.PI * envelopeHz * t));
            double tone = Math.Sin(2 * Math.PI * lowFreq * t) * 0.65 + Math.Sin(2 * Math.PI * highFreq * t) * 0.35;
            samples[i] = (short)(tone * envelope * short.MaxValue * 0.85);
        }

        int dataSize = samples.Length * sizeof(short);

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (short sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return ms.ToArray();
    }

    private static void AssertWebpSignature(byte[] imageBytes)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(imageBytes, Has.Length.GreaterThanOrEqualTo(12));
            Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
        }
    }
}
