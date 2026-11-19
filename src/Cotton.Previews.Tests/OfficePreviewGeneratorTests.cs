// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests
{
    public class OfficePreviewGeneratorTests
    {
        [TestCase("report.docx")]
        [TestCase("text.pptx")]
        public async Task GeneratePreview_RendersTextWithoutInstalledFonts(string fileName)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", fileName);
            await using FileStream source = File.OpenRead(path);
            byte[] result = await GetGenerator(fileName).GeneratePreviewWebPAsync(source, 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            int darkPixels = 0;
            for (int y = 0; y < image.Height / 3; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (pixel.A > 200 && pixel.R < 200 && pixel.G < 200 && pixel.B < 200)
                    {
                        darkPixels++;
                    }
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(image.Width, image.Height), Is.LessThanOrEqualTo(200));
                Assert.That(darkPixels, Is.GreaterThan(10), "The upper part of the page must contain text, not only the chart below it.");
                Assert.That(source.CanRead, Is.True);
                Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 255, 255, 255)));
            });
        }

        [Test]
        public async Task GeneratePreview_RendersSlideImage()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.pptx");
            await using FileStream source = File.OpenRead(path);
            byte[] result = await new PresentationPreviewGenerator().GeneratePreviewWebPAsync(source, 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            Assert.That(CountGreenPixels(image), Is.GreaterThan(100));
        }

        [TestCase("damaged.docx")]
        [TestCase("damaged.pptx")]
        public async Task GeneratePreview_RejectsDamagedDocument(string fileName)
        {
            using MemoryStream source = new("not an Office document"u8.ToArray());
            IPreviewGenerator generator = GetGenerator(fileName);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await generator.GeneratePreviewWebPAsync(source, 200));
            Assert.That(source.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreview_DoesNotReadLaterSlides()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.pptx");
            using MemoryStream source = new();
            await using (FileStream file = File.OpenRead(path))
            {
                await file.CopyToAsync(source);
            }
            using (ZipArchive package = new(source, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry slide = package.GetEntry("ppt/slides/slide2.xml")
                    ?? throw new InvalidDataException("The fixture has no second slide.");
                await using Stream output = slide.Open();
                output.SetLength(0);
                await output.WriteAsync(Encoding.UTF8.GetBytes("not valid slide XML"));
            }
            source.Position = 0;

            byte[] result = await new PresentationPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            Assert.That(Math.Max(image.Width, image.Height), Is.EqualTo(200));
            Assert.That(source.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreview_RendersJpegWithConflictingChromaPositions()
        {
            using Image<Rgba32> cover = new(80, 60, new Rgba32(30, 180, 60));
            for (int y = 0; y < cover.Height; y++)
            {
                for (int x = 31; x < cover.Width; x++)
                {
                    cover[x, y] = new Rgba32(220, 30, 70);
                }
            }
            using MemoryStream jpeg = new();
            await cover.SaveAsJpegAsync(jpeg);
            byte[] encoded = jpeg.ToArray();
            ReadOnlyMemory<byte> exif = new byte[]
            {
                0xFF, 0xE1, 0x00, 0x22, 0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
                0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x13, 0x02, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.pptx");
            using MemoryStream source = new();
            await using (FileStream file = File.OpenRead(path))
            {
                await file.CopyToAsync(source);
            }
            using (ZipArchive package = new(source, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry relationships = package.GetEntry("ppt/slides/_rels/slide1.xml.rels")
                    ?? throw new InvalidDataException("The fixture has no slide relationships.");
                string xml;
                using (StreamReader reader = new(relationships.Open()))
                {
                    xml = await reader.ReadToEndAsync();
                }
                await using (Stream output = relationships.Open())
                {
                    output.SetLength(0);
                    await output.WriteAsync(Encoding.UTF8.GetBytes(xml.Replace("image1.png", "conflict.jpeg", StringComparison.Ordinal)));
                }
                ZipArchiveEntry picture = package.CreateEntry("ppt/media/conflict.jpeg");
                await using Stream image = picture.Open();
                await image.WriteAsync(encoded);
            }
            source.Position = 0;
            byte[] expected = await new PresentationPreviewGenerator().GeneratePreviewWebPAsync(source, 200);
            using (ZipArchive package = new(source, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry picture = package.GetEntry("ppt/media/conflict.jpeg")
                    ?? throw new InvalidDataException("The JPEG part is missing.");
                await using Stream image = picture.Open();
                image.SetLength(0);
                await image.WriteAsync(encoded.AsMemory(0, 2));
                await image.WriteAsync(exif);
                await image.WriteAsync(encoded.AsMemory(2));
            }
            source.Position = 0;

            byte[] result = await new PresentationPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> preview = Image.Load<Rgba32>(result);
            Assert.That(result, Is.EqualTo(expected), "Conflicting EXIF must not change the JFIF rendering.");
            Assert.That(CountGreenPixels(preview), Is.GreaterThan(100));
        }

        private static int CountGreenPixels(Image<Rgba32> image)
        {
            int greenPixels = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (pixel.G > pixel.R + 30 && pixel.G > pixel.B + 10)
                    {
                        greenPixels++;
                    }
                }
            }
            return greenPixels;
        }

        private static IPreviewGenerator GetGenerator(string fileName) => Path.GetExtension(fileName) switch
        {
            ".docx" => new WordPreviewGenerator(),
            ".pptx" => new PresentationPreviewGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName)),
        };
    }
}
