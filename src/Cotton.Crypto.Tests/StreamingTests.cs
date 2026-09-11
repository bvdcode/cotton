// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;
using Cotton.Crypto.Tests.TestUtils;

namespace Cotton.Crypto.Tests
{
    [Category("Streaming")]
    public class StreamingTests
    {
        private static byte[] Key() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        [Test]
        public async Task EncryptDecrypt_WithNonSeekable_Streams()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 7, threads: 2);
            byte[] data = [.. Enumerable.Range(0, 500_000).Select(i => (byte)(i & 0xFF))];

            using MemoryStream inner = new MemoryStream(data);
            using NonSeekableReadStream nonSeek = new NonSeekableReadStream(inner);
            using MemoryStream encrypted = new MemoryStream();
            await cipher.EncryptAsync(nonSeek, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);

            encrypted.Position = 0;
            using MemoryStream decrypted = new MemoryStream();
            await cipher.DecryptAsync(encrypted, decrypted);

            Assert.That(decrypted.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Encrypt_Cancellation_MidPipeline_NoLeaks()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 5, threads: 2);
            byte[] data = [.. Enumerable.Range(0, AesGcmStreamCipher.MinChunkSize * 3).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using SlowWriteStream slowOut = new SlowWriteStream(new MemoryStream(), delayMs: 10);
            using CancellationTokenSource cts = new CancellationTokenSource();

            long before = GC.GetAllocatedBytesForCurrentThread();
            Task task = cipher.EncryptAsync(input, slowOut, chunkSize: AesGcmStreamCipher.MinChunkSize, ct: cts.Token);
            // cancel after small delay to let pipeline spin up
            Task.Delay(30).ContinueWith(_ => cts.Cancel());

            Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
            long after = GC.GetAllocatedBytesForCurrentThread();
            // Not a strict zero-alloc, but ensure we didn't balloon allocations by > 10MB due to leaks
            Assert.That(after - before, Is.LessThan(10L * 1024 * 1024));
        }

        [Test]
        public void Decrypt_Cancellation_MidPipeline_NoLeaks()
        {
            byte[] key = Key();
            AesGcmStreamCipher enc = new AesGcmStreamCipher(key, keyId: 6, threads: 2);
            AesGcmStreamCipher dec = new AesGcmStreamCipher(key, keyId: 6, threads: 2);
            // Use more data to ensure decrypt runs long enough to observe cancellation reliably
            byte[] data = [.. Enumerable.Range(0, AesGcmStreamCipher.MinChunkSize * 32).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream ciphertext = new MemoryStream();
            enc.EncryptAsync(input, ciphertext, chunkSize: AesGcmStreamCipher.MinChunkSize).GetAwaiter().GetResult();
            ciphertext.Position = 0;

            using SlowWriteStream slowOut = new SlowWriteStream(new MemoryStream(), delayMs: 25);
            using CancellationTokenSource cts = new CancellationTokenSource();

            long before = GC.GetAllocatedBytesForCurrentThread();
            // Kick off decryption; request cancellation soon after to interrupt mid-pipeline
            Task task = dec.DecryptAsync(ciphertext, slowOut, ct: cts.Token);
            cts.CancelAfter(50);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.LessThan(10L * 1024 * 1024));
        }

        [Test]
        public async Task HugeFile_SyntheticStream_HeaderAndIndices_LongVsInt()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 8);
            long huge = 6L * 1024 * 1024 * 1024; // 6 GB
            using SeekableSyntheticReadStream fake = new SeekableSyntheticReadStream(huge);
            using MemoryStream outEnc = new MemoryStream();
            // Only header will be written (input immediately EOF), but code path uses long for lengths
            await cipher.EncryptAsync(fake, outEnc, chunkSize: AesGcmStreamCipher.DefaultChunkSize);
            outEnc.Position = 0;
            // Should be able to parse headers without overflow
            FileHeader header = await StreamHeaderReader.ReadFileAsync(outEnc);
            Assert.That(header.TotalPlaintextLength, Is.EqualTo(huge));
        }

        [Test]
        public void HotPaths_DoNotAllocate_Significantly()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 9, threads: 2);
            byte[] data = [.. Enumerable.Range(0, AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using DevNullStream output = new DevNullStream();

            // warm-up
            cipher.EncryptAsync(input, output, chunkSize: AesGcmStreamCipher.MinChunkSize).GetAwaiter().GetResult();
            input.Position = 0;

            long before = GC.GetAllocatedBytesForCurrentThread();
            cipher.EncryptAsync(input, output, chunkSize: AesGcmStreamCipher.MinChunkSize).GetAwaiter().GetResult();
            long after = GC.GetAllocatedBytesForCurrentThread();

            // Allow small control-plane allocations (<32KB)
            Assert.That(after - before, Is.LessThan(32 * 1024));
        }
    }
}
