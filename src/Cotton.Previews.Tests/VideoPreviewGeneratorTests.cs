// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Globalization;

namespace Cotton.Previews.Tests
{
    public class VideoPreviewGeneratorTests
    {
        [TestCase("0.9")]
        [TestCase("2")]
        public async Task GeneratePreviewWebPAsync_AudioOutlastsVideo_ReturnsVideoFrame(string videoDuration)
        {
            byte[] preview = await GenerateMediaPreviewAsync(
                "-f lavfi -i color=c=blue:s=64x64:r=30 "
                + "-f lavfi -i sine=frequency=440:sample_rate=8000:duration=6 "
                + $"-map 0:v -map 1:a -c:v libx264 -t:v {videoDuration} -c:a aac",
                "mp4", "video/mp4");

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.That(image[32, 32].B, Is.GreaterThan(200));
        }

        [TestCase("mp4", "aac", "video/mp4")]
        [TestCase("webm", "libopus", "video/webm")]
        [TestCase("mp4", "aac", "audio/mp4")]
        [TestCase("webm", "libopus", "audio/webm")]
        public async Task GeneratePreviewWebPAsync_AudioOnlyContainer_ReturnsWaveform(
            string extension, string codec, string contentType)
        {
            byte[] preview = await GenerateMediaPreviewAsync(
                $"-f lavfi -i sine=frequency=440:sample_rate=8000:duration=2 -c:a {codec}",
                extension, contentType);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
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
            byte[] preview = await GenerateMediaPreviewAsync(
                "-f lavfi -i sine=frequency=440:sample_rate=8000:duration=2 "
                + "-f lavfi -i color=c=red:s=64x64:r=1 "
                + "-map 0:a -map 1:v -c:a aac -c:v mjpeg -frames:v 1 -disposition:v attached_pic",
                "mp4", contentType);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(image[32, 32].A, Is.EqualTo(255));
                Assert.That(image[32, 32].R, Is.GreaterThan(200));
            });
        }

        [TestCase(1, "webm", "libvpx-vp9")]
        [TestCase(3, "webm", "libvpx-vp9")]
        [TestCase(15, "mp4", "libx264")]
        [TestCase(150, "webm", "libvpx-vp9")]
        [TestCase(150, "mp4", "libx264")]
        public async Task GeneratePreviewWebPAsync_ShortVideo_ReturnsFrame(int frames, string extension, string codec)
        {
            await FfmpegBinary.EnsureAvailableAsync();
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"preview-video-{Guid.NewGuid():N}.{extension}");
            try
            {
                await FfmpegProcessRunner.RunAsync(
                    "-hide_banner -loglevel error -f lavfi -i color=c=blue:s=64x64:r=30 "
                        + $"-frames:v {frames.ToString(CultureInfo.InvariantCulture)} -c:v {codec} -y \"{path}\"",
                    Stream.Null, TimeSpan.FromSeconds(15), "test video");
                await using FileStream source = File.OpenRead(path);

                byte[] preview = await new VideoPreviewGenerator().GeneratePreviewWebPAsync(source, 64);

                using Image<Rgba32> image = Image.Load<Rgba32>(preview);
                Assert.Multiple(() =>
                {
                    Assert.That(image.Width, Is.EqualTo(64));
                    Assert.That(image.Height, Is.EqualTo(64));
                    Assert.That(image[32, 32].B, Is.GreaterThan(200));
                });
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static async Task<byte[]> GenerateMediaPreviewAsync(string arguments, string extension, string contentType)
        {
            await FfmpegBinary.EnsureAvailableAsync();
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"preview-media-{Guid.NewGuid():N}.{extension}");
            try
            {
                await FfmpegProcessRunner.RunAsync(
                    $"-hide_banner -loglevel error {arguments} -y \"{path}\"",
                    Stream.Null, TimeSpan.FromSeconds(15), "test media");
                await using FileStream source = File.OpenRead(path);
                IPreviewGenerator generator = PreviewGeneratorProvider.GetGeneratorByContentType(contentType)!;
                return await generator.GeneratePreviewWebPAsync(source, 64);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
