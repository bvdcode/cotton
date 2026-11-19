// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests
{
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
            using MemoryStream stream = new MemoryStream(source);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

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
            using MemoryStream stream = new MemoryStream(source);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

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
            using MemoryStream stream = new MemoryStream(source);
            stream.Position = source.Length / 2;

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.InRange(112, 113));
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_PngSource_ProducesLossyWebp_NotVp8l()
        {
            // A PNG (lossless) source must not yield a lossless VP8L preview: social
            // crawlers such as Telegram's link-preview bot cannot decode VP8L, which
            // leaves shared links showing text but no image.
            byte[] source = CreateGradientPngBytes(width: 640, height: 360);
            using MemoryStream stream = new MemoryStream(source);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            string format = Encoding.ASCII.GetString(preview, 12, 4);
            Assert.That(format, Is.Not.EqualTo("VP8L"), "Preview must be lossy WebP, not lossless VP8L.");
        }

        [TestCase(2400, 1200, 200, 100)]
        [TestCase(1200, 2400, 100, 200)]
        [TestCase(80, 60, 80, 60)]
        public async Task GeneratePreviewWebPAsync_Jpeg_KeepsAspectRatioWithoutUpscaling(
            int width, int height, int expectedWidth, int expectedHeight)
        {
            using Image<Rgba32> source = new Image<Rgba32>(width, height);
            using MemoryStream stream = new MemoryStream();
            await source.SaveAsJpegAsync(stream);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(expectedWidth));
                Assert.That(image.Height, Is.EqualTo(expectedHeight));
            });
        }

        [TestCase((ushort)6)]
        [TestCase((ushort)8)]
        public async Task GeneratePreviewWebPAsync_Jpeg_AppliesExifOrientation(ushort orientation)
        {
            using Image<Rgba32> source = new Image<Rgba32>(800, 400);
            source.Metadata.ExifProfile = new ExifProfile();
            source.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);
            using MemoryStream stream = new MemoryStream();
            await source.SaveAsJpegAsync(stream);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(100));
                Assert.That(image.Height, Is.EqualTo(200));
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_AnimatedGif_PreservesFrames()
        {
            using Image<Rgba32> source = new Image<Rgba32>(400, 200, new Rgba32(255, 0, 0));
            using Image<Rgba32> secondFrame = new Image<Rgba32>(400, 200, new Rgba32(0, 0, 255));
            source.Frames.AddFrame(secondFrame.Frames.RootFrame);
            using MemoryStream stream = new MemoryStream();
            await source.SaveAsGifAsync(stream);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(100));
                Assert.That(image.Frames.Count, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_NonSeekableStream_StillDecodes()
        {
            byte[] source = CreateGradientPngBytes(width: 640, height: 320);
            using MemoryStream compressed = new MemoryStream();
            using (GZipStream compressor = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await compressor.WriteAsync(source);
            }
            compressed.Position = 0;
            using GZipStream stream = new GZipStream(compressed, CompressionMode.Decompress);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(100));
            });
        }

        private static byte[] CreateGradientPngBytes(int width, int height)
        {
            using Image<Rgba32> image = new Image<Rgba32>(width, height);

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

            using MemoryStream ms = new MemoryStream();
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
}
