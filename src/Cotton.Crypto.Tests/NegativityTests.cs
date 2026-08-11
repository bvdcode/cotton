// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;
using Cotton.Crypto.Tests.TestUtils;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Cotton.Crypto.Tests
{
    [Category("Negativity")]
    public class NegativityTests
    {
        private const int TagSize = AesGcmStreamCipher.TagSize;
        private const int NonceSize = AesGcmStreamCipher.NonceSize;
        private const int MinChunk = AesGcmStreamCipher.MinChunkSize;

        private static byte[] ValidMasterKey() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        private static async Task<(FileHeader fileHeader, List<(ChunkHeader hdr, int cipherOffset)> chunks)> ParseAllHeadersAsync(
            byte[] encrypted)
        {
            using MemoryStream stream = new(encrypted, writable: false);
            FileHeader fileHeader = await StreamHeaderReader.ReadFileAsync(stream);
            List<(ChunkHeader, int)> chunks = [];
            while (stream.Position < stream.Length)
            {
                ChunkHeader? chunk = await StreamHeaderReader.TryReadChunkAsync(stream);
                if (chunk is null)
                {
                    break;
                }

                int cipherOffset = (int)stream.Position;
                chunks.Add((chunk.Value, cipherOffset));
                stream.Position += chunk.Value.PlaintextLength;
            }

            return (fileHeader, chunks);
        }

        [Test]
        public void Tamper_FileHeader_KeyId_ShouldFailEarly()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 12);
            byte[] data = [.. Enumerable.Range(0, MinChunk + 5_000).Select(i => (byte)(i & 0xFF))];
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            var bytes = outEnc.ToArray();
            int keyIdOffset = 4 + 4 + 8; // magic + headerLen + dataLen
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(keyIdOffset, 4), 999);

            using var tampered = new MemoryStream(bytes, writable: false);
            using var outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await cipher.DecryptAsync(tampered, outDec));
        }

        [Test]
        public void Tamper_FileHeader_EncryptedKey_ShouldFail()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 13);
            byte[] data = [.. Enumerable.Range(0, MinChunk + 1).Select(i => (byte)(i & 0xFF))];
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            var bytes = outEnc.ToArray();
            int encKeyOffset = 4 + 4 + 8 + 4 + NonceSize + TagSize; // file header layout
            bytes[encKeyOffset] ^= 0xFF;

            using var tampered = new MemoryStream(bytes, writable: false);
            using var outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
        }

        [Test]
        public async Task Tamper_Chunk_Tag_ShouldFail_NoPayload()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 15);
            byte[] data = [.. Enumerable.Range(0, MinChunk + 10_000).Select(i => (byte)(i & 0xFF))];
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            var bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThan(0));

            int headerLen = 4 + 4 + 8 + 4 + TagSize; // compact chunk header (no nonce)
            int chunk0HeaderStart = chunks[0].cipherOffset - headerLen;
            int tagOffset = chunk0HeaderStart + 4 + 4 + 8 + 4; // tag starts right after keyId
            bytes[tagOffset] ^= 0xFF;

            using var tampered = new MemoryStream(bytes, writable: false);
            using var outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
            Assert.That(outDec.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task Truncation_Fails_OnFileHeader_And_Chunk()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 2);
            byte[] data = [.. Enumerable.Range(0, MinChunk + 10_000).Select(i => (byte)(i & 0xFF))];
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            var full = outEnc.ToArray();

            // Truncate inside ciphertext of first chunk
            var (_, chunks) = await ParseAllHeadersAsync(full);
            Assert.That(chunks, Has.Count.GreaterThan(0));
            int cut = chunks[0].cipherOffset + (int)(chunks[0].hdr.PlaintextLength / 2);
            using var truncated1 = new MemoryStream(full.AsSpan(0, cut).ToArray(), writable: false);
            using var dec1 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated1, dec1));

            // Truncate mid-file-header
            int fileHeaderLen = 4 + 4 + 8 + 4 + 4 + NonceSize + TagSize + 32;
            int cut2 = fileHeaderLen / 2;
            using var truncated2 = new MemoryStream(full.AsSpan(0, cut2).ToArray(), writable: false);
            using var dec2 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated2, dec2));
        }

        [Test]
        public async Task Truncation_AfterWholeChunks_WithoutTerminator_ShouldFail()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 16);
            byte[] data = [.. Enumerable.Range(0, MinChunk * 2).Select(i => (byte)(i & 0xFF))];
            using var input = new NonSeekableReadStream(new MemoryStream(data));
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] full = outEnc.ToArray();
            var (fileHeader, chunks) = await ParseAllHeadersAsync(full);
            Assert.That(fileHeader.TotalPlaintextLength, Is.Zero);
            Assert.That(chunks, Has.Count.GreaterThan(0));
            Assert.That(chunks[^1].hdr.PlaintextLength, Is.Zero);

            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int endMarkerStart = chunks[^1].cipherOffset - headerLen;
            using var truncated = new MemoryStream(full.AsSpan(0, endMarkerStart).ToArray(), writable: false);
            using var dec = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated, dec));
        }

        [Test]
        public async Task Tamper_EndMarker_Tag_ShouldFail()
        {
            var cipher = new AesGcmStreamCipher(ValidMasterKey(), keyId: 17);
            byte[] data = [.. Enumerable.Range(0, MinChunk).Select(i => (byte)(i & 0xFF))];
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks[^1].hdr.PlaintextLength, Is.Zero);

            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int endMarkerHeaderStart = chunks[^1].cipherOffset - headerLen;
            int tagOffset = endMarkerHeaderStart + 4 + 4 + 8 + 4;
            bytes[tagOffset] ^= 0xFF;

            using var tampered = new MemoryStream(bytes, writable: false);
            using var dec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, dec));
        }
    }
}
