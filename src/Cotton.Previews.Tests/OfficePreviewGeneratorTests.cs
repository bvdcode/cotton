// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using Lokad.OoxPdf;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Previews.Tests
{
    public class OfficePreviewGeneratorTests
    {
        [TestCase("report.docx", OoxPdfInputKind.Docx)]
        [TestCase("text.pptx", OoxPdfInputKind.Pptx)]
        public async Task GeneratePreview_RendersTextWithoutInstalledFonts(string fileName, OoxPdfInputKind kind)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", fileName);
            await using FileStream source = File.OpenRead(path);
            byte[] result = await new OfficePreviewGenerator(kind, "application/octet-stream").GeneratePreviewWebPAsync(source, 200);
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
            byte[] result = await new OfficePreviewGenerator(OoxPdfInputKind.Pptx, "application/octet-stream").GeneratePreviewWebPAsync(source, 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(result);
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
            Assert.That(greenPixels, Is.GreaterThan(100));
        }

        [TestCase(OoxPdfInputKind.Docx)]
        [TestCase(OoxPdfInputKind.Pptx)]
        public async Task GeneratePreview_RejectsDamagedDocument(OoxPdfInputKind kind)
        {
            using MemoryStream source = new("not an Office document"u8.ToArray());
            OfficePreviewGenerator generator = new(kind, "application/octet-stream");
            await Assert.ThrowsAsync<InvalidDataException>(async () => await generator.GeneratePreviewWebPAsync(source, 200));
            Assert.That(source.CanRead, Is.True);
        }
    }
}
