using System.Buffers.Binary;
using System.Security.Cryptography;
using Cotton.Crypto.Models;
using Cotton.Crypto.Tests.TestUtils;

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
            var rng = new Random(seed);
            var data = new byte[len];
            rng.NextBytes(data);
            return data;
        }

        private static AesGcmStreamCipher Cipher(int keyId = 1, int? threads = null)
            => new(ValidMasterKey(), keyId, threads);

        private static AesGcmKeyHeader ReadHeader(Stream s) => AesGcmKeyHeader.FromStream(s, NonceSize, TagSize);

        private static (AesGcmKeyHeader fileHeader, List<(AesGcmKeyHeader hdr, int cipherOffset)> chunks) ParseAllHeaders(byte[] encrypted)
        {
            using var ms = new MemoryStream(encrypted, writable: false);
            var fileHeader = ReadHeader(ms);
            var chunks = new List<(AesGcmKeyHeader, int)>();
            while (ms.Position < ms.Length)
            {
                long posBefore = ms.Position;
                try
                {
                    var ch = ReadHeader(ms);
                    int cipherOffset = (int)ms.Position;
                    chunks.Add((ch, cipherOffset));
                    ms.Position += ch.DataLength;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (InvalidDataException)
                {
                    // Not a valid next header -> stop scanning
                    ms.Position = posBefore;
                    break;
                }
            }
            return (fileHeader, chunks);
        }

        private static byte[] ComposeNonceExpected(int keyId, long chunkIndex)
        {
            byte[] nonce = new byte[NonceSize];
            BinaryPrimitives.WriteUInt32LittleEndian(nonce.AsSpan(0, 4), unchecked((uint)keyId));
            BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(4, 8), unchecked((ulong)chunkIndex));
            return nonce;
        }

        [Test]
        [Repeat(20)]
        public async Task Fuzz_RoundTrip_RandomizedChunks_AndThreads()
        {
            int seed = TestContext.CurrentContext.CurrentRepeatCount * 12345 + 7;
            int dataLen = new Random(seed).Next(0, 1_000_000); // up to ~1MB
            int chunk = Math.Max(MinChunk, new Random(seed + 1).Next(MinChunk, MinChunk * 4));
            int threads = Math.Max(2, Math.Min(Environment.ProcessorCount, new Random(seed + 2).Next(1, 8)));

            byte[] data = RandomBytes(dataLen, seed + 3);
            using var input = new MemoryStream(data);
            using var encrypted = new MemoryStream();
            using var decrypted = new MemoryStream();

            var cipher = Cipher(keyId: 11, threads: threads);
            await cipher.EncryptAsync(input, encrypted, chunk);
            encrypted.Position = 0;
            await cipher.DecryptAsync(encrypted, decrypted);

            Assert.That(decrypted.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Decrypt_Fails_WithWrongMasterKey()
        {
            var mk1 = ValidMasterKey();
            var mk2 = ValidMasterKey();
            mk2[0] ^= 0xFF; // different key

            var cipher1 = new AesGcmStreamCipher(mk1, keyId: 5);
            var cipher2 = new AesGcmStreamCipher(mk2, keyId: 5);

            byte[] data = RandomBytes(300_000, 42);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher1.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using var outDec = new MemoryStream();
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher2.DecryptAsync(outEnc, outDec));
        }

        [Test]
        public void Decrypt_Fails_WithKeyIdMismatch()
        {
            var mk = ValidMasterKey();
            var enc = new AesGcmStreamCipher(mk, keyId: 10);
            var dec = new AesGcmStreamCipher(mk, keyId: 99);

            byte[] data = RandomBytes(200_000, 77);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            enc.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using var outDec = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await dec.DecryptAsync(outEnc, outDec));
        }

        [Test]
        public async Task Nonce_Composition_MatchesChunkHeaders()
        {
            int keyId = 21;
            var cipher = Cipher(keyId, threads: 3);
            byte[] data = RandomBytes(MinChunk * 2 + 111, 99);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            var bytes = outEnc.ToArray();
            var (_, chunks) = ParseAllHeaders(bytes);
            for (int i = 0; i < chunks.Count; i++)
            {
                var expected = ComposeNonceExpected(keyId, i);
                Assert.That(chunks[i].hdr.Nonce, Is.EqualTo(expected), $"Chunk {i} nonce mismatch");
            }
        }

        [Test]
        public void Truncation_Fails_OnChunkHeaderOrCiphertext()
        {
            var cipher = Cipher(keyId: 2);
            byte[] data = RandomBytes(MinChunk + 10_000, 123);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            var full = outEnc.ToArray();

            // Truncate inside ciphertext of first chunk
            var (_, chunks) = ParseAllHeaders(full);
            Assert.That(chunks, Is.Not.Empty);
            int cut = chunks[0].cipherOffset + (int)(chunks[0].hdr.DataLength / 2);
            using var truncated1 = new MemoryStream(full.AsSpan(0, cut).ToArray(), writable: false);
            using var dec1 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated1, dec1));

            // Truncate just before a chunk header
            int beforeHeader = chunks[0].cipherOffset - 1;
            using var truncated2 = new MemoryStream(full.AsSpan(0, beforeHeader).ToArray(), writable: false);
            using var dec2 = new MemoryStream();
            Assert.ThrowsAsync<EndOfStreamException>(async () => await cipher.DecryptAsync(truncated2, dec2));
        }

        [Test]
        public void Tamper_EachChunk_ShouldFail()
        {
            var cipher = Cipher(keyId: 8);
            byte[] data = RandomBytes(MinChunk * 2 + 50_000, 222);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            var bytes = outEnc.ToArray();
            var (_, chunks) = ParseAllHeaders(bytes);

            Assert.That(chunks, Is.Not.Empty);

            for (int i = 0; i < chunks.Count; i++)
            {
                var copy = (byte[])bytes.Clone();
                int offset = chunks[i].cipherOffset;
                if (chunks[i].hdr.DataLength > 0)
                {
                    copy[offset] ^= 0xFF;
                }
                using var tampered = new MemoryStream(copy, writable: false);
                using var output = new MemoryStream();
                Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, output), $"Tamper in chunk {i} should fail");
            }
        }

        [Test]
        public void Decrypt_Cancellation_Throws()
        {
            var cipher = Cipher();
            byte[] data = RandomBytes(500_000, 314);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk).GetAwaiter().GetResult();

            outEnc.Position = 0;
            using var output = new MemoryStream();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await cipher.DecryptAsync(outEnc, output, cts.Token));
        }

        [Test]
        public async Task SlowOutput_Backpressure_RoundTrip()
        {
            var cipher = Cipher(threads: 3);
            byte[] data = RandomBytes(700_000, 2718);
            using var input = new MemoryStream(data);
            using var outEnc = new MemoryStream();
            await cipher.EncryptAsync(input, outEnc, chunkSize: MinChunk);

            outEnc.Position = 0;
            using var slow = new SlowWriteStream(new MemoryStream(), delayMs: 1);
            await cipher.DecryptAsync(outEnc, slow);
            var innerMs = (MemoryStream)slow.Inner;
            innerMs.Position = 0;
            Assert.That(innerMs.ToArray(), Is.EqualTo(data));
        }
    }
}
