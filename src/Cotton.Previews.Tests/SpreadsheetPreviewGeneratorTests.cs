// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
    }
}
