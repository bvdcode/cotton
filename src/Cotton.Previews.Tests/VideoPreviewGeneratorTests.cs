// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Globalization;

namespace Cotton.Previews.Tests
{
    public class VideoPreviewGeneratorTests
    {
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
    }
}
