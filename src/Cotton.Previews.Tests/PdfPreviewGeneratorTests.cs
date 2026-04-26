// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace Cotton.Previews.Tests;

public class PdfPreviewGeneratorTests
{
    private PdfPreviewGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new PdfPreviewGenerator();
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ValidSinglePagePdf_ProducesWebpWithinRequestedBounds()
    {
        byte[] pdfBytes = CreateSinglePagePdfBytes("PDF preview test");
        using var stream = new MemoryStream(pdfBytes);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);

        Assert.That(Math.Max(image.Width, image.Height), Is.LessThanOrEqualTo(200));
    }

    [Test]
    public void GeneratePreviewWebPAsync_InvalidSize_ThrowsArgumentOutOfRangeException()
    {
        byte[] pdfBytes = CreateSinglePagePdfBytes("PDF preview invalid size");
        using var stream = new MemoryStream(pdfBytes);

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _generator.GeneratePreviewWebPAsync(stream, size: 0));
    }

    [Test]
    public void GeneratePreviewWebPAsync_InvalidPdf_Throws()
    {
        byte[] invalidBytes = Encoding.UTF8.GetBytes("not a pdf");
        using var stream = new MemoryStream(invalidBytes);

        Assert.That(async () => await _generator.GeneratePreviewWebPAsync(stream, size: 200), Throws.Exception);
    }

    private static byte[] CreateSinglePagePdfBytes(string text)
    {
        string escaped = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

        string content = $"BT /F1 24 Tf 50 140 Td ({escaped}) Tj ET";
        byte[] contentBytes = Encoding.ASCII.GetBytes(content);

        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];

        using var ms = new MemoryStream();
        var offsets = new List<long> { 0 };

        static void WriteAscii(MemoryStream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        WriteAscii(ms, "%PDF-1.4\n");

        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(ms.Position);
            WriteAscii(ms, $"{i + 1} 0 obj\n");
            WriteAscii(ms, objects[i]);
            WriteAscii(ms, "\nendobj\n");
        }

        long xrefOffset = ms.Position;

        WriteAscii(ms, $"xref\n0 {offsets.Count}\n");
        WriteAscii(ms, "0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++)
        {
            WriteAscii(ms, $"{offsets[i]:0000000000} 00000 n \n");
        }

        WriteAscii(ms, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return ms.ToArray();
    }

    private static void AssertWebpSignature(byte[] imageBytes)
    {
        Assert.That(imageBytes.Length, Is.GreaterThanOrEqualTo(12));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
    }
}
