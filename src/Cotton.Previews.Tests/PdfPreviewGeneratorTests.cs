// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;
using static Cotton.Previews.Tests.PreviewImageAssertions;

namespace Cotton.Previews.Tests
{
    public class PdfPreviewGeneratorTests
    {
        private PdfPreviewGenerator _generator = null!;

        [SetUp]
        public void SetUp()
        {
            _generator = new PdfPreviewGenerator();
        }

        [TestCase(200)]
        [TestCase(2560)]
        public async Task GeneratePreviewWebPAsync_ValidSinglePagePdf_RendersAtRequestedResolution(int size)
        {
            byte[] pdfBytes = CreateSinglePagePdfBytes("PDF preview test");
            using MemoryStream stream = new MemoryStream(pdfBytes);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(image.Width, image.Height), Is.EqualTo(size));
                Assert.That(image[0, 0].A, Is.Zero);
                Assert.That(CountVisiblePixels(image), Is.GreaterThan(50));
                Assert.That(stream.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_ExplicitBackground_ProducesOpaquePage()
        {
            using MemoryStream stream = new(CreateSinglePagePdfBytes("Opaque PDF"));

            byte[] preview = await new PdfPreviewGenerator(Color.White).GeneratePreviewWebPAsync(stream, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 255, 255, 255)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task GeneratePreviewWebPAsync_OptionalContent_RespectsDefaultVisibility(bool visible)
        {
            const string content = "1 0 0 rg 0 0 50 100 re f /OC /Layer BDC 0 1 0 rg 50 0 150 100 re f EMC";
            string state = visible ? "ON" : "OFF";
            string[] objects =
            [
                $"<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [5 0 R] /D << /BaseState /{state} >> >> >>",
                "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Properties << /Layer 5 0 R >> >> /Contents 4 0 R >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
                "<< /Type /OCG /Name (Preview layer) >>",
            ];
            using MemoryStream source = new(CreatePdfBytes(objects));

            byte[] preview = await new PdfPreviewGenerator(Color.White).GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Rgba32 layer = image[100, 50];
            Assert.Multiple(() =>
            {
                Assert.That(image[25, 50].R, Is.GreaterThan(240));
                Assert.That(image[25, 50].G, Is.LessThan(15));
                Assert.That(layer.G, Is.GreaterThan(240));
                Assert.That(layer.R, visible ? Is.LessThan(15) : Is.GreaterThan(240));
                Assert.That(layer.B, visible ? Is.LessThan(15) : Is.GreaterThan(240));
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_MultiplePages_RendersFirstPage()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Pdf", "graphics.pdf");
            await using FileStream source = File.OpenRead(path);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Rgba32 red = image[20, 180];
            Rgba32 blue = image[50, 150];
            Assert.Multiple(() =>
            {
                Assert.That(red.R, Is.GreaterThan(220));
                Assert.That(red.B, Is.LessThan(30));
                Assert.That(blue.B, Is.GreaterThan(220));
                Assert.That(blue.R, Is.LessThan(30));
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_NonSeekablePdf_RendersAndLeavesSourceOpen()
        {
            using MemoryStream compressed = new();
            await using (GZipStream encoder = new(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await encoder.WriteAsync(CreateSinglePagePdfBytes("Streamed PDF"));
            }
            compressed.Position = 0;
            await using GZipStream source = new(compressed, CompressionMode.Decompress);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, 200);

            AssertWebpSignature(preview);
            Assert.That(source.CanRead, Is.True);
        }

        [TestCase("compatibility.pdf")]
        [TestCase("scan-codecs.pdf")]
        [TestCase("scan-globals.pdf")]
        public async Task GeneratePreviewWebPAsync_SupportedPdfFeatures_RenderVisibleContent(string fileName)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Pdf", fileName);
            await using FileStream source = File.OpenRead(path);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(image.Width, image.Height), Is.LessThanOrEqualTo(200));
                Assert.That(CountVisiblePixels(image), Is.GreaterThan(100));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_UnsupportedFeatures_ThrowsWithoutPartialPreview()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Pdf", "unsupported.pdf");
            await using FileStream source = File.OpenRead(path);

            await Assert.ThrowsAsync<NotSupportedException>(() => _generator.GeneratePreviewWebPAsync(source, 200));

            Assert.That(source.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_InvalidSize_ThrowsArgumentOutOfRangeException()
        {
            byte[] pdfBytes = CreateSinglePagePdfBytes("PDF preview invalid size");
            using MemoryStream stream = new MemoryStream(pdfBytes);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _generator.GeneratePreviewWebPAsync(stream, size: 0));
        }

        [Test]
        public void GeneratePreviewWebPAsync_InvalidPdf_Throws()
        {
            byte[] invalidBytes = Encoding.UTF8.GetBytes("not a pdf");
            using MemoryStream stream = new MemoryStream(invalidBytes);

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

            return CreatePdfBytes(objects);
        }

        private static byte[] CreatePdfBytes(string[] objects)
        {
            using MemoryStream ms = new MemoryStream();
            List<long> offsets = new List<long> { 0 };

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
}
