// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Previews;
using NUnit.Framework;
using SixLabors.ImageSharp;

namespace Cotton.Server.IntegrationTests
{
    public class RawPreviewGeneratorTests
    {
        [TestCase("arw")]
        [TestCase("cr2")]
        [TestCase("cr3")]
        [TestCase("dng")]
        [TestCase("nef")]
        [TestCase("nrw")]
        [TestCase("orf")]
        [TestCase("pef")]
        [TestCase("raf")]
        [TestCase("rw2")]
        [TestCase("srw")]
        public async Task RealCameraRaw_GeneratesBothPreviewSizes(string extension)
        {
            string? fixtureDirectory = Environment.GetEnvironmentVariable("COTTON_TEST_RAW_FIXTURES_DIR");
            if (string.IsNullOrWhiteSpace(fixtureDirectory))
            {
                Assert.Ignore("Set COTTON_TEST_RAW_FIXTURES_DIR to a directory with sample.<extension> RAW files.");
            }

            string filePath = Path.Combine(fixtureDirectory, $"sample.{extension}");
            if (!File.Exists(filePath))
            {
                Assert.Ignore($"RAW fixture is unavailable: sample.{extension}.");
            }

            string contentType = FileContentTypeResolver.ResolveFromFileName(filePath);
            IPreviewGenerator? generator = PreviewGeneratorProvider.GetGeneratorByContentType(contentType);
            Assert.That(generator, Is.TypeOf<RawPreviewGenerator>());

            using FileStream source = File.OpenRead(filePath);
            byte[] small = await generator!.GeneratePreviewWebPAsync(source,
                PreviewGeneratorProvider.DefaultSmallPreviewSize);
            byte[] large = await generator.GeneratePreviewWebPAsync(source,
                PreviewGeneratorProvider.DefaultLargePreviewSize);

            ImageInfo? smallInfo = Image.Identify(small);
            ImageInfo? largeInfo = Image.Identify(large);
            Assert.That(smallInfo, Is.Not.Null);
            Assert.That(largeInfo, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(smallInfo!.Width, smallInfo.Height),
                    Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));
                Assert.That(Math.Max(largeInfo!.Width, largeInfo.Height),
                    Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                Assert.That((long)largeInfo.Width * largeInfo.Height,
                    Is.GreaterThanOrEqualTo((long)smallInfo.Width * smallInfo.Height));
            });
            if (extension == "arw")
            {
                Assert.That(largeInfo!.Height, Is.GreaterThan(largeInfo.Width));
            }
        }
    }
}
