// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Cotton.Crypto.Tests.TestUtils;

namespace Cotton.Crypto.Tests;

[Category("Streaming-Pipe")]
public class AesGcmStreamCipherPipeTests
{
    private static byte[] Key() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    [Test]
    public async Task EncryptAsync_StreamOverload_ProducesSameBytesAsDirect()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 11, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 3 * AesGcmStreamCipher.MinChunkSize + 123).Select(i => (byte)(i & 0xFF))];

        using var input1 = new MemoryStream(data);
        using var directOut = new MemoryStream();
        await cipher.EncryptAsync(input1, directOut, chunkSize: AesGcmStreamCipher.MinChunkSize);
        var directBytes = directOut.ToArray();

        using var input2 = new MemoryStream(data);
        var pipeStream = await cipher.EncryptAsync(input2, chunkSize: AesGcmStreamCipher.MinChunkSize);
        using var collected = new MemoryStream();
        await pipeStream.CopyToAsync(collected);
        var pipeBytes = collected.ToArray();

        Assert.That(pipeBytes, Is.EqualTo(directBytes));
    }

    [Test]
    public async Task RoundTrip_EncryptDecrypt_StreamOverloads()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 12, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 5 * AesGcmStreamCipher.MinChunkSize + 777).Select(i => (byte)(i & 0xFF))];

        using var input = new MemoryStream(data);
        var encStream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize);
        using var ciphertextCollected = new MemoryStream();
        await encStream.CopyToAsync(ciphertextCollected);

        ciphertextCollected.Position = 0;
        var decStream = await cipher.DecryptAsync(ciphertextCollected);
        using var plaintextCollected = new MemoryStream();
        await decStream.CopyToAsync(plaintextCollected);

        Assert.That(plaintextCollected.ToArray(), Is.EqualTo(data));
    }

    [Test]
    public void EncryptAsync_StreamOverload_Cancellation()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 13, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 10 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
        using var input = new MemoryStream(data);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() => cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize, ct: cts.Token));
    }

    [Test]
    public void DecryptAsync_StreamOverload_Cancellation()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 14, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 4 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
        using var input = new MemoryStream(data);
        using var encrypted = new MemoryStream();
        cipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize).GetAwaiter().GetResult();
        encrypted.Position = 0;

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() => cipher.DecryptAsync(encrypted, ct: cts.Token));
    }

    [Test]
    public async Task EncryptAsync_StreamOverload_NonSeekableInput()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 15);
        byte[] data = [.. Enumerable.Range(0, 500_000).Select(i => (byte)(i & 0xFF))];
        using var inner = new MemoryStream(data);
        using var nonSeek = new NonSeekableReadStream(inner);

        var stream = await cipher.EncryptAsync(nonSeek, chunkSize: AesGcmStreamCipher.MinChunkSize);
        using var collected = new MemoryStream();
        await stream.CopyToAsync(collected);

        collected.Position = 0;
        var decStream = await cipher.DecryptAsync(collected);
        using var plainCollected = new MemoryStream();
        await decStream.CopyToAsync(plainCollected);

        Assert.That(plainCollected.ToArray(), Is.EqualTo(data));
    }

    [Test]
    public async Task DecryptAsync_StreamOverload_Tamper_Throws()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 16);
        byte[] data = [.. Enumerable.Range(0, 3 * AesGcmStreamCipher.MinChunkSize + 10).Select(i => (byte)(i & 0xFF))];
        using var input = new MemoryStream(data);
        var encStream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize);
        using var ciphertextCollected = new MemoryStream();
        await encStream.CopyToAsync(ciphertextCollected);

        // Tamper a byte after headers
        var bytes = ciphertextCollected.ToArray();
        int headerLen = Cotton.Crypto.Internals.AesGcmStreamFormat.ComputeFileHeaderLength(AesGcmStreamCipher.NonceSize, AesGcmStreamCipher.TagSize, AesGcmStreamCipher.KeySize);
        if (bytes.Length > headerLen + 5)
        {
            bytes[headerLen + 5] ^= 0xFF;
        }
        using var tampered = new MemoryStream(bytes);
        Assert.ThrowsAsync<CryptographicException>(async () =>
        {
            var dec = await cipher.DecryptAsync(tampered);
            using var sink = new MemoryStream();
            await dec.CopyToAsync(sink);
        });
    }
}
