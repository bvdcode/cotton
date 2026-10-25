// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;

namespace Cotton.Previews.Tests
{
    public class HeicPreviewGeneratorTests
    {
        [TestCase("basic-8bit.heic", 32, 24)]
        [TestCase("basic-10bit.heic", 32, 24)]
        [TestCase("rotate-90.heic", 24, 32)]
        [TestCase("alpha.heic", 32, 24)]
        [TestCase("wavefront.heic", 32, 23)]
        public async Task GeneratePreviewWebPAsync_Heic_PreservesGeometry(string fileName, int width, int height)
        {
            using FileStream stream = OpenFixture(fileName);
            stream.Position = stream.Length;

            byte[] preview = await new HeicPreviewGenerator().GeneratePreviewWebPAsync(stream, 32);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image.Size, Is.EqualTo(new Size(width, height)));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_Alpha_PreservesTransparency()
        {
            using FileStream stream = OpenFixture("alpha.heic");

            byte[] preview = await new HeicPreviewGenerator().GeneratePreviewWebPAsync(stream, 32);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image[0, 0].A, Is.LessThan(byte.MaxValue));
            Assert.That(image[0, 0].A, Is.LessThan(image[8, 8].A));
        }

        [TestCase("image/jpeg")]
        [TestCase("image/png")]
        [TestCase("image/gif")]
        [TestCase("image/bmp")]
        [TestCase("image/webp")]
        [TestCase("image/tiff")]
        public async Task GeneratePreviewWebPAsync_OtherImageSignature_UsesImageGenerator(string mimeType)
        {
            using Image<Rgba32> source = new(80, 40, new Rgba32(40, 100, 200));
            Assert.That(Configuration.Default.ImageFormatsManager.TryFindFormatByMimeType(mimeType, out IImageFormat? format), Is.True);
            using MemoryStream stream = new();
            await source.SaveAsync(stream, Configuration.Default.ImageFormatsManager.GetEncoder(format!));

            byte[] expected = await new ImagePreviewGenerator().GeneratePreviewWebPAsync(stream, 32);
            stream.Position = stream.Length;
            byte[] actual = await new HeicPreviewGenerator().GeneratePreviewWebPAsync(stream, 32);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_NonSeekableImage_ReadsSignatureWithoutLosingBytes()
        {
            using Image<Rgba32> source = new(80, 40, new Rgba32(40, 100, 200));
            using MemoryStream compressed = new();
            await using (GZipStream encoder = new(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await source.SaveAsPngAsync(encoder);
            }
            compressed.Position = 0;
            await using GZipStream stream = new(compressed, CompressionMode.Decompress);

            byte[] preview = await new HeicPreviewGenerator().GeneratePreviewWebPAsync(stream, 32);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image.Size, Is.EqualTo(new Size(32, 16)));
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_UnknownSignature_StillRejectsInvalidHeic()
        {
            using MemoryStream stream = new("invalid image data"u8.ToArray());

            await Assert.ThrowsAsync<InvalidDataException>(() => new HeicPreviewGenerator().GeneratePreviewWebPAsync(stream, 32));
        }

        private static FileStream OpenFixture(string fileName) => File.OpenRead(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Heic", fileName));
    }
}
