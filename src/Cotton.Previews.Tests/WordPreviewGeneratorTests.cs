// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests
{
    public class WordPreviewGeneratorTests
    {
        [TestCase(200)]
        [TestCase(2560)]
        public async Task GeneratePreview_RendersTextImageAndTableAtRequestedSize(int size)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.docx");
            await using FileStream source = File.OpenRead(path);

            byte[] preview = await new WordPreviewGenerator().GeneratePreviewWebPAsync(source, size);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            int darkPixels = 0;
            int greenPixels = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (pixel.R < 150 && pixel.G < 150 && pixel.B < 150)
                    {
                        darkPixels++;
                    }
                    if (pixel.G > pixel.R + 20 && pixel.G > pixel.B + 10)
                    {
                        greenPixels++;
                    }
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(image.Width, image.Height), Is.EqualTo(size));
                Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 255, 255, 255)));
                Assert.That(darkPixels, Is.GreaterThan(100));
                Assert.That(greenPixels, Is.GreaterThan(100));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreview_NonSeekableDocument_LeavesSourceOpen()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.docx");
            using MemoryStream compressed = new();
            await using (GZipStream encoder = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
            await using (FileStream file = File.OpenRead(path))
            {
                await file.CopyToAsync(encoder);
            }
            compressed.Position = 0;
            await using GZipStream source = new(compressed, CompressionMode.Decompress);

            byte[] preview = await new WordPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(image.Height, Is.EqualTo(200));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreview_MultipleSections_RendersOnlyFirstPageWithItsGeometry()
        {
            const string body = """
                <w:p><w:pPr><w:sectPr><w:pgSz w:w="6000" w:h="6000"/></w:sectPr></w:pPr>
                <w:r><w:rPr><w:color w:val="FF0000"/><w:sz w:val="48"/></w:rPr><w:t>First page</w:t></w:r></w:p>
                <w:p><w:r><w:rPr><w:color w:val="0000FF"/></w:rPr><w:t>Second page</w:t></w:r></w:p>
                <w:sectPr><w:pgSz w:w="12000" w:h="6000"/></w:sectPr>
                """;
            using MemoryStream source = CreateDocument(body);

            byte[] preview = await new WordPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            int red = 0;
            int blue = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (pixel.R > 150 && pixel.G < 100 && pixel.B < 100)
                    {
                        red++;
                    }
                    if (pixel.B > 150 && pixel.R < 100 && pixel.G < 100)
                    {
                        blue++;
                    }
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(200));
                Assert.That(red, Is.GreaterThan(50));
                Assert.That(blue, Is.Zero);
            });
        }

        [Test]
        public async Task GeneratePreview_MissingPageSize_UsesA4()
        {
            using MemoryStream source = CreateDocument("<w:p><w:r><w:t>A4 page</w:t></w:r></w:p><w:sectPr/>");

            byte[] preview = await new WordPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image.Width / (double)image.Height, Is.EqualTo(210d / 297).Within(.01));
        }

        [TestCase("●")]
        [TestCase("▪")]
        [TestCase("Ж")]
        public async Task GeneratePreview_BundledCharacters_DoNotRenderMissingGlyphBox(string text)
        {
            using MemoryStream source = CreateDocument($"<w:p><w:r><w:rPr><w:sz w:val=\"72\"/></w:rPr><w:t>{text}</w:t></w:r></w:p><w:sectPr/>");
            using MemoryStream missingGlyph = CreateDocument("<w:p><w:r><w:rPr><w:sz w:val=\"72\"/></w:rPr><w:t>\U0010FFFF</w:t></w:r></w:p><w:sectPr/>");
            WordPreviewGenerator generator = new();

            byte[] preview = await generator.GeneratePreviewWebPAsync(source, 200);
            byte[] placeholder = await generator.GeneratePreviewWebPAsync(missingGlyph, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(preview, Is.Not.EqualTo(placeholder));
                Assert.That(image.Height, Is.EqualTo(200));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [TestCase("🔴")]
        [TestCase("👩‍💻")]
        [TestCase("1️⃣")]
        public async Task GeneratePreview_Emoji_RendersColorArtwork(string text)
        {
            using MemoryStream source = CreateDocument($"<w:p><w:r><w:rPr><w:sz w:val=\"144\"/></w:rPr><w:t>{text}</w:t></w:r></w:p><w:sectPr/>");

            byte[] preview = await new WordPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            int coloredPixels = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) > 40)
                    {
                        coloredPixels++;
                    }
                }
            }
            Assert.That(coloredPixels, Is.GreaterThan(20));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task GeneratePreview_InvalidSize_IsRejected(int size)
        {
            using MemoryStream source = new();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new WordPreviewGenerator().GeneratePreviewWebPAsync(source, size));
        }

        private static MemoryStream CreateDocument(string body)
        {
            MemoryStream output = new();
            using (ZipArchive package = new(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                Write("[Content_Types].xml", """
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                    <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                    <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                    </Types>
                    """);
                Write("_rels/.rels", """
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                    <Relationship Id="main" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                    </Relationships>
                    """);
                Write("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body + "</w:body></w:document>");

                void Write(string name, string xml)
                {
                    using Stream stream = package.CreateEntry(name).Open();
                    stream.Write(Encoding.UTF8.GetBytes(xml));
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
