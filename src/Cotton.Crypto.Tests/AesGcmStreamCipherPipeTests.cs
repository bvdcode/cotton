// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Tests.TestUtils;
using System.Security.Cryptography;

namespace Cotton.Crypto.Tests
{
    [Category("Streaming-Pipe")]
    public class AesGcmStreamCipherPipeTests
    {
        private static byte[] Key() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        [Test]
        public async Task EncryptAsync_StreamOverload_DecryptsCorrectly()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 11, threads: 2);
            byte[] data = [.. Enumerable.Range(0, (3 * AesGcmStreamCipher.MinChunkSize) + 123).Select(i => (byte)(i & 0xFF))];

            using MemoryStream input1 = new MemoryStream(data);
            using MemoryStream directOut = new MemoryStream();
            await cipher.EncryptAsync(input1, directOut, chunkSize: AesGcmStreamCipher.MinChunkSize);
            directOut.Position = 0;
            using MemoryStream directPlain = new MemoryStream();
            await cipher.DecryptAsync(directOut, directPlain);

            using MemoryStream input2 = new MemoryStream(data);
            Stream pipeStream = await cipher.EncryptAsync(input2, chunkSize: AesGcmStreamCipher.MinChunkSize);
            using MemoryStream collected = new MemoryStream();
            await pipeStream.CopyToAsync(collected);
            collected.Position = 0;
            using MemoryStream pipePlain = new MemoryStream();
            await cipher.DecryptAsync(collected, pipePlain);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(directPlain.ToArray(), Is.EqualTo(data));
                Assert.That(pipePlain.ToArray(), Is.EqualTo(data));
            }
        }

        [Test]
        public async Task RoundTrip_EncryptDecrypt_StreamOverloads()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 12, threads: 2);
            byte[] data = [.. Enumerable.Range(0, (5 * AesGcmStreamCipher.MinChunkSize) + 777).Select(i => (byte)(i & 0xFF))];

            using MemoryStream input = new MemoryStream(data);
            Stream encStream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize);
            using MemoryStream ciphertextCollected = new MemoryStream();
            await encStream.CopyToAsync(ciphertextCollected);

            ciphertextCollected.Position = 0;
            Stream decStream = await cipher.DecryptAsync(ciphertextCollected);
            using MemoryStream plaintextCollected = new MemoryStream();
            await decStream.CopyToAsync(plaintextCollected);

            Assert.That(plaintextCollected.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task EncryptAsync_StreamOverload_Cancellation()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 13, threads: 2);
            // More chunks, minimal chunk size to increase chance of pending read
            byte[] data = [.. Enumerable.Range(0, 16 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(25);

            Stream stream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize, ct: cts.Token);
            byte[] buffer = new byte[8 * 1024];
            OperationCanceledException? caught = null;
            long totalRead = 0;
            try
            {
                while (true)
                {
                    int r = await stream.ReadAsync(buffer, cts.Token);
                    if (r == 0) break;
                    totalRead += r;
                    await Task.Delay(5); // slow down to increase chance of cancellation
                }
            }
            catch (OperationCanceledException ex)
            {
                caught = ex;
            }

            Assert.That(caught != null || (cts.IsCancellationRequested && totalRead < data.Length), "Expected cancellation to abort or truncate encryption stream.");
        }

        [Test]
        public async Task EncryptAsync_StreamOverload_PreCanceledToken_CompletesReader()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 131, threads: 2);
            byte[] data = [.. Enumerable.Range(0, 2 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Stream stream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize, ct: cts.Token);
            using MemoryStream sink = new MemoryStream();

            Assert.That(
                async () => await stream.CopyToAsync(sink).WaitAsync(TimeSpan.FromSeconds(3)),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task DecryptAsync_StreamOverload_Cancellation()
        {
            byte[] key = Key();
            AesGcmStreamCipher encCipher = new AesGcmStreamCipher(key, keyId: 14, threads: 2);
            AesGcmStreamCipher decCipher = new AesGcmStreamCipher(key, keyId: 14, threads: 2);
            byte[] data = [.. Enumerable.Range(0, 16 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream encrypted = new MemoryStream();
            await encCipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);
            encrypted.Position = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(25);
            Stream decStream = await decCipher.DecryptAsync(encrypted, ct: cts.Token);
            byte[] buffer = new byte[8 * 1024];
            OperationCanceledException? caught = null;
            long totalRead = 0;
            try
            {
                while (true)
                {
                    int r = await decStream.ReadAsync(buffer, cts.Token);
                    if (r == 0) break;
                    totalRead += r;
                    await Task.Delay(5); // slow down to increase chance of cancellation
                }
            }
            catch (OperationCanceledException ex)
            {
                caught = ex;
            }

            Assert.That(caught != null || (cts.IsCancellationRequested && totalRead < data.Length), "Expected cancellation to abort or truncate decryption stream.");
        }

        [Test]
        public async Task DecryptAsync_StreamOverload_PreCanceledToken_CompletesReader()
        {
            byte[] key = Key();
            AesGcmStreamCipher encCipher = new AesGcmStreamCipher(key, keyId: 141, threads: 2);
            AesGcmStreamCipher decCipher = new AesGcmStreamCipher(key, keyId: 141, threads: 2);
            byte[] data = [.. Enumerable.Range(0, 2 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream encrypted = new MemoryStream();
            await encCipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);
            encrypted.Position = 0;

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            Stream stream = await decCipher.DecryptAsync(encrypted, ct: cts.Token);
            using MemoryStream sink = new MemoryStream();

            Assert.That(
                async () => await stream.CopyToAsync(sink).WaitAsync(TimeSpan.FromSeconds(3)),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task EncryptAsync_StreamOverload_NonSeekableInput()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 15);
            byte[] data = [.. Enumerable.Range(0, 500_000).Select(i => (byte)(i & 0xFF))];
            using MemoryStream inner = new MemoryStream(data);
            using NonSeekableReadStream nonSeek = new NonSeekableReadStream(inner);

            Stream stream = await cipher.EncryptAsync(nonSeek, chunkSize: AesGcmStreamCipher.MinChunkSize);
            using MemoryStream collected = new MemoryStream();
            await stream.CopyToAsync(collected);

            collected.Position = 0;
            Stream decStream = await cipher.DecryptAsync(collected);
            using MemoryStream plainCollected = new MemoryStream();
            await decStream.CopyToAsync(plainCollected);

            Assert.That(plainCollected.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task DecryptAsync_StreamOverload_Tamper_Throws()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 16);
            byte[] data = [.. Enumerable.Range(0, (3 * AesGcmStreamCipher.MinChunkSize) + 10).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            Stream encStream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize);
            using MemoryStream ciphertextCollected = new MemoryStream();
            await encStream.CopyToAsync(ciphertextCollected);

            byte[] bytes = ciphertextCollected.ToArray();
            int headerLen = Cotton.Crypto.Internals.AesGcmStreamFormat.ComputeFileHeaderLength(AesGcmStreamCipher.NonceSize, AesGcmStreamCipher.TagSize, AesGcmStreamCipher.KeySize);
            if (bytes.Length > headerLen + 5) bytes[headerLen + 5] ^= 0xFF; // corrupt
            using MemoryStream tampered = new MemoryStream(bytes);
            Stream decStream = await cipher.DecryptAsync(tampered);
            using MemoryStream sink = new MemoryStream();
            Assert.That(async () => await decStream.CopyToAsync(sink),
                Throws.TypeOf<InvalidDataException>().Or.TypeOf<CryptographicException>());
        }

        [Test]
        public async Task EncryptAsync_OutputFailure_DoesNotHang()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 17, threads: 4);
            byte[] data = [.. Enumerable.Range(0, 64 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            int fileHeaderLen = Cotton.Crypto.Internals.AesGcmStreamFormat.ComputeFileHeaderLength(
                AesGcmStreamCipher.NonceSize,
                AesGcmStreamCipher.TagSize,
                AesGcmStreamCipher.KeySize);
            using ThrowingWriteStream output = new ThrowingWriteStream(fileHeaderLen + 8);

            Assert.That(
                async () => await cipher.EncryptAsync(input, output, chunkSize: AesGcmStreamCipher.MinChunkSize).WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.InstanceOf<IOException>());
        }

        [Test]
        public async Task DecryptAsync_OutputFailure_DoesNotHang()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 18, threads: 4);
            byte[] data = [.. Enumerable.Range(0, 64 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream encrypted = new MemoryStream();
            await cipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);
            encrypted.Position = 0;
            using ThrowingWriteStream output = new ThrowingWriteStream(AesGcmStreamCipher.MinChunkSize / 2);

            Assert.That(
                async () => await cipher.DecryptAsync(encrypted, output).WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.InstanceOf<IOException>());
        }

        [Test]
        public async Task DecryptAsync_NonSeekablePartialChunkHeader_Throws()
        {
            byte[] key = Key();
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(key, keyId: 19, threads: 2);
            byte[] data = [.. Enumerable.Range(0, AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
            using MemoryStream inputInner = new MemoryStream(data);
            using NonSeekableReadStream input = new NonSeekableReadStream(inputInner);
            using MemoryStream encrypted = new MemoryStream();
            await cipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);

            byte[] bytes = encrypted.ToArray();
            int fileHeaderLen = Cotton.Crypto.Internals.AesGcmStreamFormat.ComputeFileHeaderLength(
                AesGcmStreamCipher.NonceSize,
                AesGcmStreamCipher.TagSize,
                AesGcmStreamCipher.KeySize);
            byte[] truncated = bytes[..(fileHeaderLen + 5)];
            using MemoryStream tamperedInner = new MemoryStream(truncated, writable: false);
            using NonSeekableReadStream tampered = new NonSeekableReadStream(tamperedInner);
            using MemoryStream output = new MemoryStream();

            Assert.That(
                async () => await cipher.DecryptAsync(tampered, output).WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.InstanceOf<EndOfStreamException>());
        }
    }
}
