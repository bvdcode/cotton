// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Previews;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace Cotton.Previews.Tests;

public class ImagePreviewGeneratorTests
{
    private ImagePreviewGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new ImagePreviewGenerator();
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_LargeImage_ResizesWithinBounds_AndKeepsAspectRatio()
    {
        byte[] source = CreateGradientPngBytes(width: 2400, height: 1200);
        using var stream = new MemoryStream(source);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(200));
            Assert.That(image.Height, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_SmallImage_DoesNotUpscale()
    {
        byte[] source = CreateGradientPngBytes(width: 80, height: 60);
        using var stream = new MemoryStream(source);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(80));
            Assert.That(image.Height, Is.EqualTo(60));
        });
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_SeekableStream_NotAtStart_StillReadsFromBeginning()
    {
        byte[] source = CreateGradientPngBytes(width: 640, height: 360);
        using var stream = new MemoryStream(source);
        stream.Position = source.Length / 2;

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(200));
            Assert.That(image.Height, Is.InRange(112, 113));
        });
    }

    private static byte[] CreateGradientPngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte red = (byte)((x * 255) / Math.Max(1, width - 1));
                byte green = (byte)((y * 255) / Math.Max(1, height - 1));
                byte blue = (byte)((x + y) % 256);
                image[x, y] = new Rgba32(red, green, blue, 255);
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static void AssertWebpSignature(byte[] imageBytes)
    {
        Assert.That(imageBytes.Length, Is.GreaterThanOrEqualTo(12));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
    }
}
