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
    public class SpreadsheetPreviewGeneratorTests
    {
        [TestCase("xlsx")]
        [TestCase("xls")]
        public async Task GeneratePreview_RendersWorkbookGrid(string extension)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report." + extension);
            await using FileStream source = File.OpenRead(path);
            byte[] result = await new SpreadsheetPreviewGenerator().GeneratePreviewWebPAsync(source, 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            Assert.Multiple(() =>
            {
                Assert.That(result.AsSpan(0, 4).ToArray(), Is.EqualTo("RIFF"u8.ToArray()));
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(150));
                Assert.That(source.CanRead, Is.True);
            });
            int darkPixels = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y].R < 200)
                    {
                        darkPixels++;
                    }
                }
            }
            Assert.That(darkPixels, Is.GreaterThan(300), "The thumbnail must contain cell text and grid lines.");
        }

        [Test]
        public async Task GeneratePreview_PreservesSparseCellCoordinates()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.xlsx");
            using MemoryStream source = new();
            await using (FileStream file = File.OpenRead(path))
            {
                await file.CopyToAsync(source);
            }
            using (ZipArchive package = new(source, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry sheet = package.GetEntry("xl/worksheets/sheet1.xml")
                    ?? throw new InvalidDataException("The fixture has no first worksheet.");
                await using Stream output = sheet.Open();
                output.SetLength(0);
                await output.WriteAsync(Encoding.UTF8.GetBytes("""
                    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <sheetData>
                        <row r="3"><c r="C3" t="inlineStr"><is><t>Sparse</t></is></c></row>
                        <row r="5"><c r="F5" t="inlineStr"><is><t>Value</t></is></c></row>
                      </sheetData>
                    </worksheet>
                    """));
            }
            source.Position = 0;

            byte[] result = await new SpreadsheetPreviewGenerator().GeneratePreviewWebPAsync(source, 640);

            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            Assert.Multiple(() =>
            {
                Assert.That(CountDarkPixels(image, 244, 64, 80, 20), Is.GreaterThan(10), "C3 must stay in column C.");
                Assert.That(CountDarkPixels(image, 52, 64, 80, 20), Is.Zero, "Missing A3 must stay blank.");
                Assert.That(CountDarkPixels(image, 244, 92, 80, 20), Is.Zero, "The missing fourth row must stay blank.");
                Assert.That(CountDarkPixels(image, 532, 120, 80, 20), Is.GreaterThan(10), "F5 must stay two rows below C3.");
            });
        }

        [Test]
        public async Task GeneratePreview_RendersDistinctCyrillicLetters()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report.xlsx");
            using MemoryStream source = new();
            await using (FileStream file = File.OpenRead(path))
            {
                await file.CopyToAsync(source);
            }
            using (ZipArchive package = new(source, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry sheet = package.GetEntry("xl/worksheets/sheet1.xml")
                    ?? throw new InvalidDataException("The fixture has no first worksheet.");
                await using Stream output = sheet.Open();
                output.SetLength(0);
                await output.WriteAsync(Encoding.UTF8.GetBytes("""
                    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <sheetData>
                        <row r="1">
                          <c r="A1" t="inlineStr"><is><t>Б</t></is></c>
                          <c r="B1" t="inlineStr"><is><t>Ж</t></is></c>
                        </row>
                      </sheetData>
                    </worksheet>
                    """));
            }
            source.Position = 0;

            byte[] result = await new SpreadsheetPreviewGenerator().GeneratePreviewWebPAsync(source, 640);

            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            bool differentGlyphs = false;
            for (int y = 65; y < 85; y++)
            {
                for (int x = 52; x < 132; x++)
                {
                    differentGlyphs |= (image[x, y].R < 160) != (image[x + 96, y].R < 160);
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(CountDarkPixels(image, 52, 65, 80, 20), Is.GreaterThan(10));
                Assert.That(CountDarkPixels(image, 148, 65, 80, 20), Is.GreaterThan(10));
                Assert.That(differentGlyphs, Is.True, "Different Cyrillic letters must not render as the same missing glyph.");
            });
        }

        private static int CountDarkPixels(Image<Rgba32> image, int left, int top, int width, int height)
        {
            int count = 0;
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x++)
                {
                    if (image[x, y].R < 160)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
