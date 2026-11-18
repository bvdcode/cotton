// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;
using Cotton.Crypto.Tests.TestUtils;
using System.Security.Cryptography;

namespace Cotton.Crypto.Tests
{
    [Category("Streaming")]
    public class EncryptionFramingTests
    {
        [TestCase(0, AesGcmStreamCipher.MinChunkSize, 1)]
        [TestCase(17, AesGcmStreamCipher.MinChunkSize, 1)]
        [TestCase(AesGcmStreamCipher.MinChunkSize - 1, AesGcmStreamCipher.MinChunkSize, 4093)]
        [TestCase(AesGcmStreamCipher.MinChunkSize, AesGcmStreamCipher.MinChunkSize, 4093)]
        [TestCase(AesGcmStreamCipher.MinChunkSize + 17, AesGcmStreamCipher.MinChunkSize, 4093)]
        [TestCase(2 * AesGcmStreamCipher.MinChunkSize, AesGcmStreamCipher.MinChunkSize, 4093)]
        [TestCase((2 * AesGcmStreamCipher.DefaultChunkSize) + 17, AesGcmStreamCipher.DefaultChunkSize, 131591)]
        public async Task Encrypt_ShortReads_PreserveConfiguredBlockSize(int length, int chunkSize, int readSize)
        {
            byte[] data = RandomNumberGenerator.GetBytes(length);
            using AesGcmStreamCipher cipher = new(RandomNumberGenerator.GetBytes(AesGcmStreamCipher.KeySize), threads: 2);
            using MemoryStream source = new(data, writable: false);
            using FragmentedReadStream input = new(source, readSize);
            using MemoryStream encrypted = new();

            await cipher.EncryptAsync(input, encrypted, chunkSize).WaitAsync(TimeSpan.FromSeconds(15));

            encrypted.Position = 0;
            await StreamHeaderReader.ReadFileAsync(encrypted);
            for (int offset = 0; offset < length; offset += chunkSize)
            {
                ChunkHeader block = await StreamHeaderReader.ReadChunkAsync(encrypted);
                int expected = Math.Min(chunkSize, length - offset);
                Assert.That(block.PlaintextLength, Is.EqualTo(expected));
                encrypted.Position += block.PlaintextLength;
            }
            ChunkHeader endMarker = await StreamHeaderReader.ReadChunkAsync(encrypted);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(endMarker.PlaintextLength, Is.Zero);
                Assert.That(encrypted.Position, Is.EqualTo(encrypted.Length));
            }

            encrypted.Position = 0;
            using MemoryStream restored = new();
            await cipher.DecryptAsync(encrypted, restored).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.That(restored.ToArray(), Is.EqualTo(data));
        }
    }
}
