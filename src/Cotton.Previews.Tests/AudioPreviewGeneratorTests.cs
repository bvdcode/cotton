// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Buffers.Binary;
using System.Text;

namespace Cotton.Previews.Tests;

public class AudioPreviewGeneratorTests
{
    private const int LargeAudioPreviewThresholdBytes = 50 * 1024 * 1024;

    [Test]
    public void Version_ForcesReprocessingAfterWaveformTimeoutIncrease()
    {
        var generator = new AudioPreviewGenerator();

        Assert.That(generator.Version, Is.EqualTo(3));
    }

    [Test]
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
    public async Task GeneratePreviewWebPAsync_LargeWavWithoutCover_GeneratesWaveformPreview_AndWritesArtifact()
    {
        var generator = new AudioPreviewGenerator();
        byte[] wavBytes = CreateLargePcm16MonoWavBytes(minimumSizeBytes: LargeAudioPreviewThresholdBytes + (1024 * 1024));
        using var stream = new MemoryStream(wavBytes);

        byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);
        Assert.That(Math.Max(image.Width, image.Height), Is.LessThanOrEqualTo(200));
        Assert.That(CountNonTransparentPixels(image), Is.GreaterThan(0));

        string artifactsDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        string artifactPath = Path.Combine(artifactsDirectory, "audio-waveform-preview-large.webp");
        await File.WriteAllBytesAsync(artifactPath, preview);

        TestContext.Progress.WriteLine($"Large audio bytes: {wavBytes.Length:N0}");
        TestContext.Progress.WriteLine($"Large audio waveform preview artifact: {artifactPath}");
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_WavWithoutCover_SavesDebugFrames_ForVisualInspection()
    {
        var generator = new AudioPreviewGenerator();
        string artifactsDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        (string Name, int LowFreq, int HighFreq)[] cases =
        [
            ("case-a", 180, 540),
            ("case-b", 220, 660),
            ("case-c", 300, 900),
        ];

        foreach (var (Name, LowFreq, HighFreq) in cases)
        {
            byte[] wavBytes = CreatePcm16MonoWavBytes(
                sampleRate: 8000,
                durationSeconds: 2,
                lowFreq: LowFreq,
                highFreq: HighFreq);

            using var stream = new MemoryStream(wavBytes);
            byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            string artifactPath = Path.Combine(artifactsDirectory, $"audio-waveform-preview-{Name}.webp");
            await File.WriteAllBytesAsync(artifactPath, preview);
            TestContext.Progress.WriteLine($"Audio waveform preview debug artifact: {artifactPath}");
        }
    }

    private static byte[] CreatePcm16MonoWavBytes(
        int sampleRate,
        int durationSeconds,
        double lowFreq = 220,
        double highFreq = 660)
    {
        int totalSamples = sampleRate * durationSeconds;
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double envelope = 0.25 + (0.75 * Math.Abs(Math.Sin(2 * Math.PI * 0.7 * t)));
            double tone = (Math.Sin(2 * Math.PI * lowFreq * t) * 0.65) + (Math.Sin(2 * Math.PI * highFreq * t) * 0.35);
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

    private static byte[] CreateLargePcm16MonoWavBytes(int minimumSizeBytes)
    {
        const int sampleRate = 8000;
        int dataSize = AlignToEven(minimumSizeBytes - 44);
        int totalSamples = dataSize / sizeof(short);

        using var ms = new MemoryStream(44 + dataSize);
        WritePcm16MonoWavHeader(ms, sampleRate, dataSize);

        byte[] data = new byte[dataSize];
        for (int i = 0; i < totalSamples; i++)
        {
            double position = (double)i / totalSamples;
            double envelope = 0.15 + (0.85 * Math.Abs(Math.Sin(position * Math.PI * 6)));
            short sample = ((i / 8) % 2 == 0 ? short.MaxValue : short.MinValue);
            BinaryPrimitives.WriteInt16LittleEndian(
                data.AsSpan(i * sizeof(short), sizeof(short)),
                (short)(sample * envelope));
        }

        ms.Write(data);
        return ms.ToArray();
    }

    private static int AlignToEven(int value) => value % 2 == 0 ? value : value + 1;

    private static void WritePcm16MonoWavHeader(Stream stream, int sampleRate, int dataSize)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

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
    }

    private static int CountNonTransparentPixels(Image<Rgba32> image)
    {
        int count = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].A > 0)
                    {
                        count++;
                    }
                }
            }
        });

        return count;
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
