// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;
using System.Xml;
using static Cotton.Previews.Tests.PreviewImageAssertions;

namespace Cotton.Previews.Tests
{
    public class SvgPreviewGeneratorTests
    {
        private SvgPreviewGenerator _generator = null!;

        [SetUp]
        public void SetUp()
        {
            _generator = new SvgPreviewGenerator();
        }

        [TestCase(200)]
        [TestCase(2560)]
        public async Task GeneratePreviewWebPAsync_Svg_RendersWithinBounds(int size)
        {
            byte[] source = Encoding.UTF8.GetBytes("""
            <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="600" viewBox="0 0 1200 600">
              <rect width="1200" height="600" fill="#1E40AF" />
              <circle cx="300" cy="300" r="220" fill="#22C55E" />
              <rect x="600" y="100" width="500" height="400" fill="#F59E0B" />
            </svg>
            """);

            using MemoryStream stream = new MemoryStream(source);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(image.Width, Is.EqualTo(size));
                Assert.That(image.Height, Is.EqualTo(size / 2));
                Assert.That(stream.CanRead, Is.True);
            }
        }

        [TestCase("vector-preview.svg", 200)]
        [TestCase("effects-preview.svg", 200)]
        [TestCase("effects-preview.svg", 2560)]
        [TestCase("editor-metadata.svg", 200)]
        [TestCase("icon.svg", 200)]
        [TestCase("icon-monochrome.svg", 200)]
        public async Task GeneratePreviewWebPAsync_SupportedScene_RendersVisibleContent(string fileName, int size)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Svg", fileName);
            await using FileStream source = File.OpenRead(path);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, size);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(image.Width, image.Height), Is.EqualTo(size));
                Assert.That(CountVisiblePixels(image), Is.GreaterThan(100));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_CyrillicText_RendersWithoutSystemFonts()
        {
            byte[] svg = Encoding.UTF8.GetBytes("""
                <svg xmlns="http://www.w3.org/2000/svg" width="240" height="100">
                  <text x="10" y="65" font-family="Canada1500" font-size="40" fill="red">Привет</text>
                </svg>
                """);
            using MemoryStream source = new(svg);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(CountVisiblePixels(image), Is.GreaterThan(100));
                Assert.That(image[0, 0].A, Is.Zero);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_NonSeekableSvg_RendersAndLeavesSourceOpen()
        {
            const string svg = """
                <svg xmlns="http://www.w3.org/2000/svg" width="120" height="60">
                  <rect x="30" y="10" width="60" height="40" fill="red" />
                </svg>
                """;
            using MemoryStream compressed = new(CreateSvgzBytes(svg));
            using GZipStream source = new(compressed, CompressionMode.Decompress);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(source, 200);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            Assert.Multiple(() =>
            {
                Assert.That(image[100, 50].R, Is.GreaterThan(200));
                Assert.That(image[100, 50].A, Is.EqualTo(255));
                Assert.That(image[0, 0].A, Is.Zero);
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_SvgzBeyondExpandedLimit_IsRejected()
        {
            string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" + new string(' ', 16 * 1024 * 1024) + "</svg>";
            using MemoryStream source = new(CreateSvgzBytes(svg));

            await Assert.ThrowsAsync<InvalidDataException>(() => _generator.GeneratePreviewWebPAsync(source, 200));

            Assert.That(source.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_UnsupportedFeature_IsRejected()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("""
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><foreignObject /></svg>
                """));

            await Assert.ThrowsAsync<NotSupportedException>(() => _generator.GeneratePreviewWebPAsync(source, 200));
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_ExternalEntity_IsRejected()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("""
                <!DOCTYPE svg [<!ENTITY external SYSTEM "file:///not-available">]>
                <svg xmlns="http://www.w3.org/2000/svg">&external;</svg>
                """));

            await Assert.ThrowsAsync<XmlException>(() => _generator.GeneratePreviewWebPAsync(source, 200));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task GeneratePreviewWebPAsync_InvalidSize_IsRejected(int size)
        {
            using MemoryStream source = new();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _generator.GeneratePreviewWebPAsync(source, size));
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_Svgz_RendersWithinBounds()
        {
            string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="900" height="600" viewBox="0 0 900 600">
              <rect width="900" height="600" fill="#0EA5E9" />
              <rect x="80" y="80" width="740" height="440" fill="#111827" />
            </svg>
            """;

            byte[] source = CreateSvgzBytes(svg);
            using MemoryStream stream = new MemoryStream(source);

            byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(133));
            }
        }

        private static byte[] CreateSvgzBytes(string svgContent)
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(svgContent);
                gzip.Write(inputBytes, 0, inputBytes.Length);
            }

            return output.ToArray();
        }

        private static void AssertWebpSignature(byte[] imageBytes)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(imageBytes, Has.Length.GreaterThanOrEqualTo(12));
                Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
                Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
            }
        }
    }
}
