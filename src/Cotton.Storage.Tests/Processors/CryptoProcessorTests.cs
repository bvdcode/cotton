// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Processors;
using EasyExtensions.Crypto;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Processors
{
    [TestFixture]
    public class CryptoProcessorTests
    {
        private AesGcmStreamCipher _cipher = null!;
        private CryptoProcessor _processor = null!;

        [SetUp]
        public void Setup()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(key, keyId: 1, threads: null);
            _processor = new CryptoProcessor(_cipher);
        }

        [TearDown]
        public void TearDown()
        {
            _cipher?.Dispose();
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_EmptyStream_ReturnsOriginal()
        {
            // Arrange
            var originalData = Array.Empty<byte>();
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_OneByte_ReturnsOriginal()
        {
            // Arrange
            var originalData = "*"u8.ToArray();
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_SmallData_ReturnsOriginal()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Hello, World!");
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1KB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024];
            for (int i = 0; i < originalData.Length; i++)
            {
                originalData[i] = (byte)(i % 256);
            }
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1MB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024 * 1024];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_RandomData_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[4096];
            RandomNumberGenerator.Fill(originalData);
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = new MemoryStream();
            await decrypted.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_Encrypt_ChangesData()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Sensitive Data");
            var originalStream = new MemoryStream(originalData);

            // Act
            var encrypted = await _processor.WriteAsync("test-uid", originalStream);

            // Assert
            var encryptedData = new MemoryStream();
            await encrypted.CopyToAsync(encryptedData);
            Assert.That(encryptedData.ToArray(), Is.Not.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_WriteAsync_ReturnsStreamAtPositionZero()
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
        public async Task CryptoProcessor_ReadAsync_ReturnsStreamAtPositionZero()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data");
            var stream = new MemoryStream(data);
            var encrypted = await _processor.WriteAsync("test-uid", stream);

            // Act
            var result = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            Assert.That(result.Position, Is.EqualTo(0));
        }

        [Test]
        public void CryptoProcessor_Priority_IsCorrect()
        {
            // Assert
            Assert.That(_processor.Priority, Is.EqualTo(1000));
        }
    }
}
