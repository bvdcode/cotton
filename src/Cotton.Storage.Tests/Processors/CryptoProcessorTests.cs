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

        private static async Task<byte[]> ReadAllAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_EmptyStream_ReturnsOriginal()
        {
            // Arrange
            var originalData = Array.Empty<byte>();
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_OneByte_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[] { 42 };
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_SmallData_ReturnsOriginal()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Hello, World!");
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1KB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024];
            for (int i = 0; i < originalData.Length; i++) originalData[i] = (byte)(i % 256);
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_1MB_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[1024 * 1024];
            RandomNumberGenerator.Fill(originalData);
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_RoundTrip_RandomData_ReturnsOriginal()
        {
            // Arrange
            var originalData = new byte[4096];
            RandomNumberGenerator.Fill(originalData);
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var decrypted = await _processor.ReadAsync("test-uid", encrypted);

            // Assert
            var result = await ReadAllAsync(decrypted);
            Assert.That(result, Is.EqualTo(originalData));
        }

        [Test]
        public async Task CryptoProcessor_Encrypt_ChangesData()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Sensitive Data");
            var encrypted = await _processor.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var encryptedBytes = await ReadAllAsync(encrypted);

            // Assert
            Assert.That(encryptedBytes, Is.Not.EqualTo(originalData));
        }

        [Test]
        public void CryptoProcessor_Priority_IsCorrect()
        {
            // Assert
            Assert.That(_processor.Priority, Is.EqualTo(1000));
        }
    }
}
