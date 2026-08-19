// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;
using Cotton.Crypto.Tests.TestUtils;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Cotton.Crypto.Tests
{
    [Category("Stability")]
    public class StabilityTests
    {
        private const int TagSize = AesGcmStreamCipher.TagSize;
        private const int NonceSize = AesGcmStreamCipher.NonceSize;
        private const int MinChunk = AesGcmStreamCipher.MinChunkSize;

        private static byte[] ValidMasterKey() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        private static byte[] RandomBytes(int len, int seed)
        {
            Random rng = new Random(seed);
            byte[] data = new byte[len];
            rng.NextBytes(data);
            return data;
        }

        private static AesGcmStreamCipher Cipher(int keyId = 1, int? threads = null)
            => new(ValidMasterKey(), keyId, threads);
        [Test]
        public void Truncation_Fails_OnFileHeader()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 3);
            byte[] data = RandomBytes(MinChunk + 123, 55);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            byte[] full = outEnc.ToArray();
            int fileHeaderLen = 4 + 4 + 8 + 4 + 4 + NonceSize + TagSize + 32; // with 32-byte encrypted key
            int cut = fileHeaderLen / 2; // mid-file-header
            using MemoryStream truncated = new MemoryStream(full.AsSpan(0, cut).ToArray(), writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated, outDec));
        }

        [Test]
        public void Tamper_FileHeader_NoncePrefix_ShouldFailEarly_NoPayload()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 17);
            byte[] data = RandomBytes(MinChunk + 10_000, 106);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            byte[] bytes = outEnc.ToArray();
            int noncePrefixOffset = 4 + 4 + 8 + 4; // magic + headerLen + dataLen + keyId
            bytes[noncePrefixOffset] ^= 0xFF; // flip a byte in noncePrefix

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
            Assert.That(outDec.Length, Is.Zero);
        }

        [Test]
        public async Task Tamper_ChunkHeader_Length_ShouldFail_NoPayload()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 18);
            byte[] data = RandomBytes((MinChunk * 2) + 999, 107);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThan(0));
            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int c0HeaderStart = chunks[0].cipherOffset - headerLen;
            // Flip the MSB of length (little-endian last byte) to force invalid length
            int lengthMsbOffset = c0HeaderStart + 4 + 4 + 7; // magic(4) + headerLen(4) + length(8)
            bytes[lengthMsbOffset] ^= 0xFF;

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await cipher.DecryptAsync(tampered, outDec));
            Assert.That(outDec.Length, Is.Zero);
        }

        [Test]
        public async Task Tamper_ChunkHeader_KeyId_ShouldFail_NoPayload()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 19);
            byte[] data = RandomBytes((MinChunk * 2) + 777, 108);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThan(0));
            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int c0HeaderStart = chunks[0].cipherOffset - headerLen;
            int keyIdOffset = c0HeaderStart + 4 + 4 + 8; // after magic(4), headerLen(4), length(8)
            bytes[keyIdOffset] ^= 0xFF;

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await cipher.DecryptAsync(tampered, outDec));
            Assert.That(outDec.Length, Is.Zero);
        }

        [Test]
        public async Task Duplicate_SecondChunk_ShouldFail_WithoutWritingUnauthenticatedPayload()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 20);
            byte[] data = RandomBytes((MinChunk * 2) + 123, 109);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));

            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int c0Start = chunks[0].cipherOffset - headerLen;
            int c0Total = headerLen + (int)chunks[0].hdr.PlaintextLength;
            int c1Start = chunks[1].cipherOffset - headerLen;
            int c1Total = headerLen + (int)chunks[1].hdr.PlaintextLength;

            // Build a duplicate chunk1 = chunk0 stream (same size region)
            byte[] dup = (byte[])bytes.Clone();
            // If lengths differ, limit copy to min to avoid overflow; remaining bytes unchanged but will still fail auth
            int copyLen = Math.Min(c0Total, c1Total);
            Array.Copy(dup, c0Start, dup, c1Start, copyLen);

            using MemoryStream tampered = new MemoryStream(dup, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
            AssertAuthenticatedPrefixOnly(outDec, data, (int)chunks[0].hdr.PlaintextLength);
        }

        [Test]
        public async Task Skip_SecondChunk_ShouldFail_WithoutWritingUnauthenticatedPayload()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 22);
            byte[] data = RandomBytes((MinChunk * 3) + 321, 110);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(3));

            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int c0Start = chunks[0].cipherOffset - headerLen;
            int c0Total = headerLen + (int)chunks[0].hdr.PlaintextLength;
            int c1Start = chunks[1].cipherOffset - headerLen;
            int c1Total = headerLen + (int)chunks[1].hdr.PlaintextLength;

            byte[] skipped = new byte[bytes.Length - c1Total];
            Array.Copy(bytes, 0, skipped, 0, c1Start);
            Array.Copy(bytes, c1Start + c1Total, skipped, c1Start, bytes.Length - (c1Start + c1Total));

            using MemoryStream tampered = new MemoryStream(skipped, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
            AssertAuthenticatedPrefixOnly(outDec, data, (int)chunks[0].hdr.PlaintextLength);
        }

        private static void AssertAuthenticatedPrefixOnly(MemoryStream actual, byte[] expected, int authenticatedPrefixLength)
        {
            byte[] bytes = actual.ToArray();
            Assert.That(bytes.Length, Is.AnyOf(0, authenticatedPrefixLength));
            Assert.That(bytes, Is.EqualTo(expected.AsSpan(0, bytes.Length).ToArray()));
        }

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
        [Repeat(50)]
        public async Task Fuzz_RoundTrip_RandomizedChunks_AndThreads()
        {
            int seed = (TestContext.CurrentContext.CurrentRepeatCount * 12345) + 7;
            int dataLen = new Random(seed).Next(0, 1_000_000); // up to ~1MB
            int chunk = Math.Max(MinChunk, new Random(seed + 1).Next(MinChunk, MinChunk * 4));
            int threads = Math.Max(2, Math.Min(Environment.ProcessorCount, new Random(seed + 2).Next(1, 8)));

            byte[] data = RandomBytes(dataLen, seed + 3);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream encrypted = new MemoryStream();
            using MemoryStream decrypted = new MemoryStream();

            AesGcmStreamCipher cipher = Cipher(keyId: 11, threads: threads);
            await cipher.EncryptAsync(input, encrypted, chunk);
            encrypted.Position = 0;
            await cipher.DecryptAsync(encrypted, decrypted);

            Assert.That(decrypted.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Decrypt_Fails_WithWrongMasterKey()
        {
            byte[] mk1 = ValidMasterKey();
            byte[] mk2 = ValidMasterKey();
            mk2[0] ^= 0xFF; // different key

            AesGcmStreamCipher cipher1 = new AesGcmStreamCipher(mk1, keyId: 5);
            AesGcmStreamCipher cipher2 = new AesGcmStreamCipher(mk2, keyId: 5);

            byte[] data = RandomBytes(300_000, 42);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher1.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher2.DecryptAsync(outEnc, outDec));
        }

        [Test]
        public void Decrypt_Fails_WithKeyIdMismatch()
        {
            byte[] mk = ValidMasterKey();
            AesGcmStreamCipher enc = new AesGcmStreamCipher(mk, keyId: 10);
            AesGcmStreamCipher dec = new AesGcmStreamCipher(mk, keyId: 99);

            byte[] data = RandomBytes(200_000, 77);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            enc.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await dec.DecryptAsync(outEnc, outDec));
        }

        [Test]
        public async Task Truncation_Fails_OnChunkHeaderOrCiphertext()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 2);
            byte[] data = RandomBytes(MinChunk + 10_000, 123);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] full = outEnc.ToArray();

            // Truncate inside ciphertext of first chunk
            var (_, chunks) = await ParseAllHeadersAsync(full);
            Assert.That(chunks, Has.Count.GreaterThan(0));
            int cut = chunks[0].cipherOffset + (int)(chunks[0].hdr.PlaintextLength / 2);
            using MemoryStream truncated1 = new MemoryStream(full.AsSpan(0, cut).ToArray(), writable: false);
            using MemoryStream dec1 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated1, dec1));

            // Truncate just before a chunk header
            int beforeHeader = chunks[0].cipherOffset - 1;
            using MemoryStream truncated2 = new MemoryStream(full.AsSpan(0, beforeHeader).ToArray(), writable: false);
            using MemoryStream dec2 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated2, dec2));
        }

        [Test]
        public async Task Tamper_EachChunk_ShouldFail()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 8);
            byte[] data = RandomBytes((MinChunk * 2) + 50_000, 222);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);

            Assert.That(chunks, Has.Count.GreaterThan(0));

            for (int i = 0; i < chunks.Count; i++)
            {
                byte[] copy = (byte[])bytes.Clone();
                int offset = chunks[i].cipherOffset;
                if (chunks[i].hdr.PlaintextLength > 0)
                {
                    copy[offset] ^= 0xFF;
                }
                else
                {
                    int headerLen = 4 + 4 + 8 + 4 + TagSize;
                    int tagOffset = offset - headerLen + 4 + 4 + 8 + 4;
                    copy[tagOffset] ^= 0xFF;
                }
                using MemoryStream tampered = new MemoryStream(copy, writable: false);
                using MemoryStream output = new MemoryStream();
                Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, output), $"Tamper in chunk {i} should fail");
            }
        }

        [Test]
        public void Decrypt_Cancellation_Throws()
        {
            AesGcmStreamCipher cipher = Cipher();
            byte[] data = RandomBytes(500_000, 314);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using MemoryStream output = new MemoryStream();
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await cipher.DecryptAsync(outEnc, output, ct: cts.Token));
        }

        [Test]
        public async Task SlowOutput_Backpressure_RoundTrip()
        {
            AesGcmStreamCipher cipher = Cipher(threads: 3);
            byte[] data = RandomBytes(700_000, 2718);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            outEnc.Position = 0;
            using SlowWriteStream slow = new SlowWriteStream(new MemoryStream(), delayMs: 1);
            await cipher.DecryptAsync(outEnc, slow);
            MemoryStream innerMs = (MemoryStream)slow.Inner;
            innerMs.Position = 0;
            Assert.That(innerMs.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Tamper_FileHeader_KeyId_ShouldFailEarly()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 12);
            byte[] data = RandomBytes(MinChunk + 5_000, 100);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            byte[] bytes = outEnc.ToArray();
            int keyIdOffset = 4 + 4 + 8; // magic + headerLen + dataLen
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(keyIdOffset, 4), 999);

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await cipher.DecryptAsync(tampered, outDec));
        }

        [Test]
        public void Tamper_FileHeader_EncryptedKey_ShouldFail()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 13);
            byte[] data = RandomBytes(MinChunk + 1, 101);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            byte[] bytes = outEnc.ToArray();
            int encKeyOffset = 4 + 4 + 8 + 4 + NonceSize + TagSize; // file header layout
            bytes[encKeyOffset] ^= 0xFF;

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
        }

        [Test]
        public void Tamper_FileHeader_TagOrNonce_ShouldFail()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 14);
            byte[] data = RandomBytes(MinChunk + 2, 102);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            byte[] original = outEnc.ToArray();

            // Flip a byte in tag
            byte[] tagTampered = (byte[])original.Clone();
            int tagOffset = 4 + 4 + 8 + 4 + NonceSize; // after nonce
            tagTampered[tagOffset] ^= 0xFF;
            using MemoryStream s1 = new MemoryStream(tagTampered, writable: false);
            using MemoryStream o1 = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(s1, o1));

            // Flip a byte in nonce
            byte[] nonceTampered = (byte[])original.Clone();
            int nonceOffset = 4 + 4 + 8 + 4; // start of nonce
            nonceTampered[nonceOffset] ^= 0xFF;
            using MemoryStream s2 = new MemoryStream(nonceTampered, writable: false);
            using MemoryStream o2 = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(s2, o2));
        }

        [Test]
        public async Task Tamper_Chunk_Tag_ShouldFail()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 15);
            byte[] data = RandomBytes(MinChunk + 10_000, 103);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThan(0));

            int headerLen = 4 + 4 + 8 + 4 + TagSize; // compact chunk header (no nonce)
            int chunk0HeaderStart = chunks[0].cipherOffset - headerLen;
            int tagOffset = chunk0HeaderStart + 4 + 4 + 8 + 4; // tag starts right after keyId
            bytes[tagOffset] ^= 0xFF;

            using MemoryStream tampered = new MemoryStream(bytes, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
            Assert.That(outDec.Length, Is.Zero);
        }

        [Test]
        public async Task Tamper_Reorder_FirstTwoChunks_ShouldFail()
        {
            AesGcmStreamCipher cipher = Cipher(keyId: 16);
            byte[] data = RandomBytes((MinChunk * 2) + 123, 104);
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            byte[] bytes = outEnc.ToArray();
            var (_, chunks) = await ParseAllHeadersAsync(bytes);
            Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2), "Need at least 2 chunks for reorder test");

            int headerLen = 4 + 4 + 8 + 4 + TagSize;
            int c0Start = chunks[0].cipherOffset - headerLen;
            int c0Total = headerLen + (int)chunks[0].hdr.PlaintextLength;
            int c1Start = chunks[1].cipherOffset - headerLen;
            int c1Total = headerLen + (int)chunks[1].hdr.PlaintextLength;

            byte[] swapped = new byte[bytes.Length];
            Array.Copy(bytes, 0, swapped, 0, c0Start);
            Array.Copy(bytes, c1Start, swapped, c0Start, c1Total);
            Array.Copy(bytes, c0Start, swapped, c0Start + c1Total, c0Total);
            int tailSrc = c1Start + c1Total;
            int tailDst = c0Start + c1Total + c0Total;
            Array.Copy(bytes, tailSrc, swapped, tailDst, bytes.Length - tailSrc);

            using MemoryStream tampered = new MemoryStream(swapped, writable: false);
            using MemoryStream outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, outDec));
        }
    }
}
