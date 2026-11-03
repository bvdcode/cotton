// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;
using static Cotton.Previews.Tests.PreviewImageAssertions;

namespace Cotton.Previews.Tests
{
    public class TgsPreviewGeneratorTests
    {
        private const string Animation = """
            {
              "v":"5.12.2", "w":200, "h":100, "fr":30, "ip":10, "op":70, "assets":[],
              "layers":[
                {"ty":1,"ip":10,"op":25,"sw":200,"sh":100,"sc":"#ff0000",
                 "ks":{"p":{"k":[0,0]},"o":{"k":100}}},
                {"ty":1,"ip":25,"op":55,"sw":200,"sh":100,"sc":"#0000ff",
                 "ks":{"p":{"k":[0,0]},"o":{"k":100}}},
                {"ty":1,"ip":55,"op":70,"sw":200,"sh":100,"sc":"#00ff00",
                 "ks":{"p":{"k":[0,0]},"o":{"k":100}}}
              ]
            }
            """;

        [TestCase(200, false)]
        [TestCase(2560, false)]
        [TestCase(200, true)]
        [TestCase(2560, true)]
        public async Task GeneratePreviewWebPAsync_NonSquareAnimation_RendersCenteredMiddleFrame(int size, bool withExpression)
        {
            string animation = Animation;
            if (withExpression)
            {
                animation = animation.Replace("\"k\":[0,0]", "\"k\":[0,0],\"x\":\"throw new Error('Must not execute')\"", StringComparison.Ordinal);
            }
            using MemoryStream source = new(await CompressAsync(Encoding.UTF8.GetBytes(animation)));

            byte[] preview = await new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, size);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Rgba32 middle = image[size / 2, size / 2];
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(size));
                Assert.That(image.Height, Is.EqualTo(size));
                Assert.That(middle.B, Is.GreaterThan(220));
                Assert.That(middle.R, Is.LessThan(30));
                Assert.That(middle.A, Is.EqualTo(255));
                Assert.That(image[size / 2, 0].A, Is.Zero);
                Assert.That(image[size / 2, size - 1].A, Is.Zero);
                Assert.That(source.CanRead, Is.True);
            });
        }

        [TestCase("orbit.tgs")]
        [TestCase("effects.tgs")]
        [TestCase("geometry.tgs")]
        public async Task GeneratePreviewWebPAsync_SupportedEffects_RenderVisibleContent(string fileName)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Tgs", fileName);
            await using FileStream source = File.OpenRead(path);

            byte[] preview = await new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(200));
                Assert.That(CountVisiblePixels(image), Is.GreaterThan(100));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_NonSeekableTgs_RendersAndLeavesSourceOpen()
        {
            byte[] tgs = await CompressAsync(Encoding.UTF8.GetBytes(Animation));
            using MemoryStream compressed = new(await CompressAsync(tgs));
            using GZipStream source = new(compressed, CompressionMode.Decompress);

            byte[] preview = await new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(image[100, 100].B, Is.GreaterThan(220));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_UnsupportedLayer_IsRejected()
        {
            string unsupported = Animation.Replace("\"ty\":1", "\"ty\":2", StringComparison.Ordinal);
            using MemoryStream source = new(await CompressAsync(Encoding.UTF8.GetBytes(unsupported)));

            await Assert.ThrowsAsync<NotSupportedException>(() => new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, 200));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task GeneratePreviewWebPAsync_InvalidSize_IsRejected(int size)
        {
            using MemoryStream source = new();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new TgsPreviewGenerator().GeneratePreviewWebPAsync(source, size));
        }

        private static async Task<byte[]> CompressAsync(byte[] bytes)
        {
            using MemoryStream output = new();
            await using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                await gzip.WriteAsync(bytes);
            }
            return output.ToArray();
        }
    }
}
