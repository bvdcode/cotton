// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Previews.Tests
{
    public class VideoPreviewGeneratorTests
    {
        [TestCase("shorter-video.mp4")]
        [TestCase("longer-video.mp4")]
        [TestCase("video-1.webm")]
        [TestCase("video-3.webm")]
        [TestCase("video-15.mp4")]
        [TestCase("video-150.webm")]
        [TestCase("video-150.mp4")]
        public async Task GeneratePreviewWebPAsync_Video_ReturnsFrame(string fixture)
        {
            string contentType = fixture.EndsWith(".webm", StringComparison.Ordinal) ? "video/webm" : "video/mp4";
            using Image<Rgba32> image = await GenerateMediaPreviewAsync(fixture, contentType);
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(64));
                Assert.That(image.Height, Is.EqualTo(64));
                Assert.That(image[32, 32].B, Is.GreaterThan(200));
            });
        }

        [TestCase("audio.mp4", "video/mp4")]
        [TestCase("audio.webm", "video/webm")]
        [TestCase("audio.mp4", "audio/mp4")]
        [TestCase("audio.webm", "audio/webm")]
        [TestCase("mulaw.wav", "audio/wav")]
        [TestCase("ms-adpcm.wav", "audio/wav")]
        public async Task GeneratePreviewWebPAsync_AudioOnlyContainer_ReturnsWaveform(string fixture, string contentType)
        {
            using Image<Rgba32> image = await GenerateMediaPreviewAsync(fixture, contentType);
            Assert.Multiple(() =>
            {
                Assert.That(image[0, 0].A, Is.Zero);
                Assert.That(PreviewImageAssertions.CountVisiblePixels(image), Is.GreaterThan(0));
            });
        }

        [TestCase("video/mp4")]
        [TestCase("audio/mp4")]
        public async Task GeneratePreviewWebPAsync_AudioWithCover_ReturnsCover(string contentType)
        {
            using Image<Rgba32> image = await GenerateMediaPreviewAsync("audio-with-cover.mp4", contentType);
            Assert.Multiple(() =>
            {
                Assert.That(image[32, 32].A, Is.EqualTo(255));
                Assert.That(image[32, 32].R, Is.GreaterThan(200));
            });
        }

        private static async Task<Image<Rgba32>> GenerateMediaPreviewAsync(string fixture, string contentType)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Media", fixture);
            await using FileStream source = File.OpenRead(path);
            IPreviewGenerator generator = PreviewGeneratorProvider.GetGeneratorByContentType(contentType)!;
            byte[] preview = await generator.GeneratePreviewWebPAsync(source, 64);
            Assert.That(source.CanRead, Is.True);
            return Image.Load<Rgba32>(preview);
        }
    }
}
