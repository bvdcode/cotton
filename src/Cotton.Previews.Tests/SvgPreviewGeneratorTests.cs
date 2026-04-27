// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests;

public class SvgPreviewGeneratorTests
{
    private SvgPreviewGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new SvgPreviewGenerator();
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_Svg_RendersWithinBounds()
    {
        byte[] source = Encoding.UTF8.GetBytes("""
            <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="600" viewBox="0 0 1200 600">
              <rect width="1200" height="600" fill="#1E40AF" />
              <circle cx="300" cy="300" r="220" fill="#22C55E" />
              <rect x="600" y="100" width="500" height="400" fill="#F59E0B" />
            </svg>
            """);

        using var stream = new MemoryStream(source);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.Width, Is.EqualTo(200));
            Assert.That(image.Height, Is.EqualTo(100));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_Svgz_RendersWithinBounds()
    {
        string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="900" height="600" viewBox="0 0 900 600">
              <rect width="900" height="600" fill="#0EA5E9" />
              <rect x="80" y="80" width="740" height="440" fill="#111827" />
            </svg>
            """;

        byte[] source = CreateSvgzBytes(svg);
        using var stream = new MemoryStream(source);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.Width, Is.EqualTo(200));
            Assert.That(image.Height, Is.EqualTo(133));
        }
    }

    private static byte[] CreateSvgzBytes(string svgContent)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(svgContent);
            gzip.Write(inputBytes, 0, inputBytes.Length);
        }

        return output.ToArray();
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
