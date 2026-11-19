// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;
using Mediana.Drawing;
using SixLabors.ImageSharp;
using ImagePixel = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews.Tests
{
    public class DrawingPreviewTests
    {
        [Test]
        public async Task TextPreviews_ConcurrentRendersMatchIndependentResults()
        {
            string[] inputs =
            [
                "First document\nSecond line\nThird line",
                "Текст на русском\n0123456789",
                "A\tB\r\nC\rD",
                "",
                new string('\0', 128),
                new string('M', 4096),
            ];
            byte[][] expected = new byte[inputs.Length][];
            for (int i = 0; i < inputs.Length; i++)
            {
                expected[i] = await RenderTextAsync(inputs[i]);
            }
            byte[][] actual = await Task.WhenAll(inputs.Select(input => Task.Run(() => RenderTextAsync(input))));
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"Concurrent render {i} changed its contents.");
            }
            Assert.That(actual[0], Is.Not.EqualTo(actual[1]));
        }

        [Test]
        public async Task LargeTextPreview_KeepsContentInsidePageMargins()
        {
            byte[] bytes = await RenderTextAsync("Readable text\nSecond line", 2560);
            using Image<ImagePixel> image = Image.Load<ImagePixel>(bytes);
            int inkPixels = 0;
            int minX = image.Width;
            int maxX = 0;
            int minY = image.Height;
            int maxY = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y].R < 100)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                        inkPixels++;
                    }
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(inkPixels, Is.GreaterThan(1000));
                Assert.That(minX, Is.GreaterThanOrEqualTo(140));
                Assert.That(maxX, Is.LessThan(2420));
                Assert.That(minY, Is.GreaterThanOrEqualTo(190));
                Assert.That(maxY, Is.LessThan(600));
            });
        }

        [TestCase("xls")]
        [TestCase("xlsx")]
        public async Task SpreadsheetPreview_LargeRequestKeepsGridResolution(string extension)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Office", "report." + extension);
            await using FileStream source = File.OpenRead(path);
            byte[] bytes = await new SpreadsheetPreviewGenerator().GeneratePreviewWebPAsync(source, 2560);
            using Image<ImagePixel> image = Image.Load<ImagePixel>(bytes);
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(640));
                Assert.That(image.Height, Is.EqualTo(480));
                Assert.That(source.CanRead, Is.True);
                Assert.That(image[400, 20].R, Is.InRange(235, 255));
                Assert.That(image[400, 450].R, Is.GreaterThan(245));
                Assert.That(image[48, 75].R, Is.LessThan(230));
            });
        }

        [TestCase(200)]
        [TestCase(2560)]
        public void Waveform_UsesAmplitudeWithTransparentAntialiasedEdges(int size)
        {
            using PixelBuffer quiet = new(size, size);
            quiet.Clear(new Rgba32(0, 0, 0, 0));
            WaveformPreviewRenderer.Draw(quiet, new float[20]);
            using PixelBuffer loud = new(size, size);
            loud.Clear(new Rgba32(0, 0, 0, 0));
            WaveformPreviewRenderer.Draw(loud, Enumerable.Repeat(1f, 20).ToArray());
            using Image<ImagePixel> image = Image.WrapMemory<ImagePixel>(loud.Pixels, size, size);
            int quietPixels = CountVisible(quiet);
            int loudPixels = CountVisible(loud);
            bool hasAntialiasing = false;
            for (int i = 3; i < loud.Pixels.Length; i += 4)
            {
                byte alpha = loud.Pixels.Span[i];
                hasAntialiasing |= alpha is > 0 and < 255;
            }
            Assert.Multiple(() =>
            {
                Assert.That(quietPixels, Is.GreaterThan(0));
                Assert.That(loudPixels, Is.GreaterThan(quietPixels * 5));
                Assert.That(hasAntialiasing, Is.True);
                Assert.That(image[0, 0].A, Is.Zero);
                Assert.That(image[size - 1, size - 1].A, Is.Zero);
                Assert.That(image[size / 2, 0].A, Is.Zero);
            });
        }

        private static int CountVisible(PixelBuffer pixels)
        {
            int count = 0;
            for (int i = 3; i < pixels.Pixels.Length; i += 4)
            {
                if (pixels.Pixels.Span[i] > 0)
                {
                    count++;
                }
            }
            return count;
        }

        private static async Task<byte[]> RenderTextAsync(string text, int size = 200)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(text));
            byte[] result = await new TextPreviewGenerator().GeneratePreviewWebPAsync(stream, size);
            Assert.That(stream.CanRead, Is.True);
            return result;
        }
    }
}
