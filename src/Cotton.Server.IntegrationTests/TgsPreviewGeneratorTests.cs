// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Previews;
using Cotton.Server.IntegrationTests.Common;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Server.IntegrationTests
{
    public class TgsPreviewGeneratorTests
    {
        [Test]
        public async Task GeneratePreview_RendersGzippedLottieWithUnicodeMetadata()
        {
            using MemoryStream source = new(await TgsTestDocument.CreateAsync());
            TgsPreviewGenerator generator = new();
            byte[] image = await generator.GeneratePreviewWebPAsync(source, 200);
            using Image<Rgba32> bitmap = Image.Load<Rgba32>(image);

            Assert.Multiple(() =>
            {
                Assert.That(generator.SupportedContentTypes, Does.Contain(AnimatedStickerContentTypes.Tgs));
                Assert.That(image.AsSpan(0, 4).ToArray(), Is.EqualTo("RIFF"u8.ToArray()));
                Assert.That(bitmap.Width, Is.EqualTo(200));
                Assert.That(bitmap.Height, Is.EqualTo(200));
                Assert.That(bitmap[100, 100].R, Is.GreaterThan(200));
                Assert.That(bitmap[100, 100].A, Is.EqualTo(255));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreview_InvalidGzipIsRejected()
        {
            using MemoryStream source = new("not a sticker"u8.ToArray());
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, 200));
        }

        [Test]
        public async Task GeneratePreview_ExcessiveExpandedJsonIsRejected()
        {
            byte[] bytes = await TgsTestDocument.CreateAsync(new string('x', (8 * 1024 * 1024) + 1));
            using MemoryStream source = new(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, 200));
        }
    }
}
