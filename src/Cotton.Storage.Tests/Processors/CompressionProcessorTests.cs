// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Processors;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Processors
{
    [TestFixture]
    public class CompressionProcessorTests
    {
        private CompressionProcessor _processor = null!;

        [SetUp]
        public void Setup()
        {
            _processor = new CompressionProcessor();
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_EmptyStream_ReturnsOriginal()
        {
            // Arrange
            var originalData = Array.Empty<byte>();
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_OneByte_ReturnsOriginal()
        {
            // Arrange
            var originalData = "*"u8.ToArray();
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_SmallData_ReturnsOriginal()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Hello, World!");
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_1KB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024];
            for (int i = 0; i < originalData.Length; i++)
            {
                originalData[i] = (byte)(i % 256);
            }
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_1MB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024 * 1024];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_RoundTrip_RandomData_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[4096];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var decompressed = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            var result = new MemoryStream();
            await decompressed.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CompressionProcessor_WriteAsync_ReturnsReadableStream()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);

            // Act
            var result = await _processor.WriteAsync("test-uid", stream);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.EqualTo(Stream.Null));
            Assert.That(result.CanRead, Is.True);
        }

        [Test]
        public async Task CompressionProcessor_ReadAsync_ReturnsReadableStream()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);
            var compressed = await _processor.WriteAsync("test-uid", stream);

            // Act
            var result = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.EqualTo(Stream.Null));
            Assert.That(result.CanRead, Is.True);
        }

        [Test]
        public async Task CompressionProcessor_WriteAsync_StreamPositionZero()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);

            // Act
            var result = await _processor.WriteAsync("test-uid", stream);

            // Assert
            Assert.That(result.Position, Is.EqualTo(0));
        }

        [Test]
        public async Task CompressionProcessor_ReadAsync_StreamPositionZero()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);
            var compressed = await _processor.WriteAsync("test-uid", stream);

            // Act
            var result = await _processor.ReadAsync("test-uid", compressed);

            // Assert
            Assert.That(result.Position, Is.EqualTo(0));
        }

        [Test]
        public async Task CompressionProcessor_CompressibleData_ReducesSize()
        {
            // Arrange - highly compressible data (repeated pattern)
            var originalData = new byte[10000];
            for (int i = 0; i < originalData.Length; i++)
            {
                originalData[i] = (byte)(i % 10);
            }
            var originalStream = new MemoryStream(originalData);

            // Act
            var compressed = await _processor.WriteAsync("test-uid", originalStream);
            var compressedLength = compressed.Length;

            // Assert
            Assert.That(compressedLength, Is.LessThan(originalData.Length), 
                "Compressed data should be smaller than original for compressible data");
        }

        [Test]
        public void CompressionProcessor_Priority_IsCorrect()
        {
            // Assert
            Assert.That(_processor.Priority, Is.EqualTo(10000));
        }
    }
}
