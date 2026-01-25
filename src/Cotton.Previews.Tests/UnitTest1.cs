// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace Cotton.Previews.Tests
{
    [TestFixture]
    public class TextPreviewGeneratorTests
    {
        private TextPreviewGenerator _generator = null!;

        [SetUp]
        public void Setup()
        {
            _generator = new TextPreviewGenerator();
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_SimpleText_GeneratesNonEmptyImage()
        {
            // Arrange
            string testText = "Hello World!\nThis is a test.\nLine 3\nLine 4";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testText));

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            Assert.That(webpData.Length, Is.GreaterThan(0));

            using var image = Image.Load<Rgba32>(webpData);
            Assert.That(image.Width, Is.EqualTo(256));
            Assert.That(image.Height, Is.EqualTo(256));

            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Image should contain non-white pixels (rendered text)");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_EmptyFile_GeneratesImageWithEmptyFileText()
        {
            // Arrange
            using var stream = new MemoryStream([]);

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            Assert.That(webpData.Length, Is.GreaterThan(0));

            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Empty file should render '(empty file)' text");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_LongText_TruncatesCorrectly()
        {
            // Arrange
            var sb = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                sb.AppendLine($"Line {i}: This is a very long line with lots of text to test truncation behavior.");
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            Assert.That(webpData.Length, Is.GreaterThan(0));

            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Long text should render visible content");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_CodeSnippet_RendersCorrectly()
        {
            // Arrange
            string codeSnippet = @"public class Example
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(codeSnippet));

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Code snippet should render visible text");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_WhitespaceOnly_GeneratesEmptyFileText()
        {
            // Arrange
            string whitespace = "   \n\n\t\t  \n   ";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(whitespace));

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Whitespace-only should render '(empty file)' text");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_SingleCharacter_RendersCorrectly()
        {
            // Arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("A"));

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Single character should render");
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_DifferentSizes_GeneratesCorrectDimensions()
        {
            // Arrange
            string testText = "Test text for size validation";
            int[] sizes = [128, 256, 512];

            foreach (int size in sizes)
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testText));

                // Act
                byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size);

                // Assert
                using var image = Image.Load<Rgba32>(webpData);
                Assert.That(image.Width, Is.EqualTo(size), $"Width should be {size}");
                Assert.That(image.Height, Is.EqualTo(size), $"Height should be {size}");
            }
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_SeekableStream_ResetsPosition()
        {
            // Arrange
            string testText = "Hello World";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testText));
            stream.Position = 5; // Move position to middle

            // Act
            byte[] webpData = await _generator.GeneratePreviewWebPAsync(stream, size: 256);

            // Assert
            Assert.That(webpData, Is.Not.Null);
            using var image = Image.Load<Rgba32>(webpData);
            bool hasNonWhitePixels = HasNonWhitePixels(image);
            Assert.That(hasNonWhitePixels, Is.True, "Should render full text after seeking to start");
        }

        private static bool HasNonWhitePixels(Image<Rgba32> image)
        {
            int nonWhiteCount = 0;
            int totalSampled = 0;
            const int sampleStep = 4; // Sample every 4th pixel for performance

            for (int y = 0; y < image.Height; y += sampleStep)
            {
                for (int x = 0; x < image.Width; x += sampleStep)
                {
                    totalSampled++;
                    Rgba32 pixel = image[x, y];
                    if (pixel.R < 250 || pixel.G < 250 || pixel.B < 250)
                    {
                        nonWhiteCount++;
                    }
                }
            }

            // At least 1% of sampled pixels should be non-white (text)
            return nonWhiteCount > (totalSampled * 0.01);
        }
    }
}

