// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Cotton.Crypto.Tests.TestUtils;
using Cotton.Crypto.Models;

namespace Cotton.Crypto.Tests;

[Category("Streaming-Pipe")]
public class AesGcmStreamCipherPipeTests
{
    private static byte[] Key() => [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    [Test]
    public async Task EncryptAsync_StreamOverload_DecryptsCorrectly()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 11, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 3 * AesGcmStreamCipher.MinChunkSize + 123).Select(i => (byte)(i & 0xFF))];

        // Direct encrypt
        using var input1 = new MemoryStream(data);
        using var directOut = new MemoryStream();
        await cipher.EncryptAsync(input1, directOut, chunkSize: AesGcmStreamCipher.MinChunkSize);
        directOut.Position = 0;
        using var directPlain = new MemoryStream();
        await cipher.DecryptAsync(directOut, directPlain);

        // Streaming encrypt
        using var input2 = new MemoryStream(data);
        var pipeStream = await cipher.EncryptAsync(input2, chunkSize: AesGcmStreamCipher.MinChunkSize);
        using var collected = new MemoryStream();
        await pipeStream.CopyToAsync(collected);
        collected.Position = 0;
        using var pipePlain = new MemoryStream();
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
    public async Task EncryptAsync_StreamOverload_Cancellation()
    {
        var key = Key();
        var cipher = new AesGcmStreamCipher(key, keyId: 13, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 12 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
        using var input = new MemoryStream(data);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(30); // let pipeline start

        var stream = await cipher.EncryptAsync(input, chunkSize: AesGcmStreamCipher.MinChunkSize, ct: cts.Token);
        byte[] buffer = new byte[64 * 1024];
        TaskCanceledException? caught = null;
        try
        {
            while (true)
            {
                int r = await stream.ReadAsync(buffer, cts.Token);
                if (r == 0) break; // finished before cancel
            }
        }
        catch (TaskCanceledException ex)
        {
            caught = ex;
        }
        Assert.That(caught, Is.Not.Null, "Expected TaskCanceledException during streaming encrypt read.");
    }

    [Test]
    public async Task DecryptAsync_StreamOverload_Cancellation()
    {
        var key = Key();
        var encCipher = new AesGcmStreamCipher(key, keyId: 14, threads: 2);
        var decCipher = new AesGcmStreamCipher(key, keyId: 14, threads: 2);
        byte[] data = [.. Enumerable.Range(0, 12 * AesGcmStreamCipher.MinChunkSize).Select(i => (byte)(i & 0xFF))];
        using var input = new MemoryStream(data);
        using var encrypted = new MemoryStream();
        await encCipher.EncryptAsync(input, encrypted, chunkSize: AesGcmStreamCipher.MinChunkSize);
        encrypted.Position = 0;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(30);
        var decStream = await decCipher.DecryptAsync(encrypted, ct: cts.Token);
        byte[] buffer = new byte[64 * 1024];
        TaskCanceledException? caught = null;
        try
        {
            while (true)
            {
                int r = await decStream.ReadAsync(buffer, cts.Token);
                if (r == 0) break; // finished before cancel
            }
        }
        catch (TaskCanceledException ex)
        {
            caught = ex;
        }
        Assert.That(caught, Is.Not.Null, "Expected TaskCanceledException during streaming decrypt read.");
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

        var bytes = ciphertextCollected.ToArray();
        int headerLen = Cotton.Crypto.Internals.AesGcmStreamFormat.ComputeFileHeaderLength(AesGcmStreamCipher.NonceSize, AesGcmStreamCipher.TagSize, AesGcmStreamCipher.KeySize);
        if (bytes.Length > headerLen + 5) bytes[headerLen + 5] ^= 0xFF; // corrupt
        using var tampered = new MemoryStream(bytes);
        var decStream = await cipher.DecryptAsync(tampered);
        using var sink = new MemoryStream();
        // Expect either InvalidDataException (header parsing) or CryptographicException (auth failure) on copy
        Assert.That(async () => await decStream.CopyToAsync(sink),
            Throws.TypeOf<InvalidDataException>().Or.TypeOf<CryptographicException>());
    }
}
