// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Processors;
using EasyExtensions.Abstractions;
using NUnit.Framework;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Processors
{
    [TestFixture]
    public class CryptoProcessorTests
    {
        [Test]
        public async Task CryptoProcessor_RoundTrip_EmptyStream_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = Array.Empty<byte>();
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_OneByte_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = "*"u8.ToArray();
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_SmallData_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = Encoding.UTF8.GetBytes("Hello, World!");
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1KB_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = new byte[1024];
            for (int i = 0; i < originalData.Length; i++)
            {
                originalData[i] = (byte)(i % 256);
            }
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1MB_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = new byte[1024 * 1024];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_RandomData_ReturnsOriginal()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var originalData = new byte[4096];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await processor.WriteAsync("test-uid", originalStream);
            var decrypted = await processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_WriteAsync_ReturnsStreamAtPositionZero()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupEncryptMock(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);

            // Act
            var result = await processor.WriteAsync("test-uid", stream);

            // Assert
            Assert.That(result.Position, Is.EqualTo(0));
        }

        [Test]
        public async Task CryptoProcessor_ReadAsync_ReturnsStreamAtPositionZero()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            SetupDecryptMock(mockCipher);
            
            var processor = new CryptoProcessor(mockCipher.Object);
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);

            // Act
            var result = await processor.ReadAsync("test-uid", stream);

            // Assert
            Assert.That(result.Position, Is.EqualTo(0));
        }

        [Test]
        public void CryptoProcessor_Priority_IsCorrect()
        {
            // Arrange
            var mockCipher = new Mock<IStreamCipher>();
            var processor = new CryptoProcessor(mockCipher.Object);

            // Assert
            Assert.That(processor.Priority, Is.EqualTo(1000));
        }

        private static void SetupRoundTripCipher(Mock<IStreamCipher> mockCipher)
        {
            // Simple XOR cipher for testing round-trip
            mockCipher.Setup(c => c.EncryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => XorStream(s, 0xAA));
            
            mockCipher.Setup(c => c.DecryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => XorStream(s, 0xAA));
        }

        private static void SetupEncryptMock(Mock<IStreamCipher> mockCipher)
        {
            mockCipher.Setup(c => c.EncryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => PassthroughStream(s));
        }

        private static void SetupDecryptMock(Mock<IStreamCipher> mockCipher)
        {
            mockCipher.Setup(c => c.DecryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => PassthroughStream(s));
        }

        private static MemoryStream XorStream(Stream input, byte key)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            var bytes = ms.ToArray();
            
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= key;
            }
            
            return new MemoryStream(bytes) { Position = 0 };
        }

        private static MemoryStream PassthroughStream(Stream input)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
    }
}
