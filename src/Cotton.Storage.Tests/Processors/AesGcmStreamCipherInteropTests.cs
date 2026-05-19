// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using EasyExtensions.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Processors;

[TestFixture]
public class AesGcmStreamCipherInteropTests
{
    private const string BrowserTwoChunkContainerHex =
        "43544e315400000020000000000000000100000001020304101112131415161718191a1b" +
        "105a66b023aeb952c3b16a92055c1160dd5f3ab5ed6c9c1462dca2b6a3d4c7fc" +
        "67e1fcbdaf77e1065f4058dce2e9ea64" +
        "43544e3124000000100000000000000001000000063de420397031cdab567aa89be7f18d" +
        "86d0074dab5ecff2aa2da23f6adee0ef" +
        "43544e3124000000100000000000000001000000c5ef24a09009a9ed0ccc6edd0daa845f" +
        "92687f5a3ed888916cd851a46a61f41e";

    private const string EasyExtensionsSingleChunkContainerHex =
        "43544e315400000020000000000000000100000001020304101112131415161718191a1b" +
        "105a66b023aeb952c3b16a92055c1160dd5f3ab5ed6c9c1462dca2b6a3d4c7fc" +
        "67e1fcbdaf77e1065f4058dce2e9ea64" +
        "43544e3124000000200000000000000001000000f7cae02a9b0ba5f0b205f417676e555a" +
        "86d0074dab5ecff2aa2da23f6adee0ef2bf2fe613fd6de9c493e03d29e28cda1";

    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes(
        "0123456789abcdefABCDEFGHIJKLMNOP");

    [Test]
    public async Task DecryptAsync_ReadsBrowserTwoChunkGoldenVector()
    {
        using var cipher = new AesGcmStreamCipher(MasterKeyBytes(), keyId: 1, threads: 1);
        await using var input = new MemoryStream(Convert.FromHexString(BrowserTwoChunkContainerHex));
        await using var output = new MemoryStream();

        await cipher.DecryptAsync(input, output);

        Assert.That(output.ToArray(), Is.EqualTo(Plaintext));
    }

    [Test]
    public async Task EncryptAsync_WritesEasyExtensionsSingleChunkGoldenVector()
    {
        using var rng = new ScriptedRandomNumberGenerator(
            FileKeyBytes(),
            [0x01, 0x02, 0x03, 0x04],
            FileKeyNonceBytes());
        using var cipher = new AesGcmStreamCipher(
            MasterKeyBytes(),
            keyId: 1,
            threads: 1,
            rng: rng);
        await using var input = new MemoryStream(Plaintext);
        await using var output = new MemoryStream();

        await cipher.EncryptAsync(
            input,
            output,
            chunkSize: AesGcmStreamCipher.MinChunkSize);

        Assert.That(
            Convert.ToHexString(output.ToArray()).ToLowerInvariant(),
            Is.EqualTo(EasyExtensionsSingleChunkContainerHex));
    }

    [Test]
    public async Task DecryptAsync_ReadsEasyExtensionsSingleChunkGoldenVector()
    {
        using var cipher = new AesGcmStreamCipher(MasterKeyBytes(), keyId: 1, threads: 1);
        await using var input = new MemoryStream(Convert.FromHexString(EasyExtensionsSingleChunkContainerHex));
        await using var output = new MemoryStream();

        await cipher.DecryptAsync(input, output);

        Assert.That(output.ToArray(), Is.EqualTo(Plaintext));
    }

    private static byte[] MasterKeyBytes() =>
        Enumerable.Range(0, AesGcmStreamCipher.KeySize)
            .Select(index => (byte)index)
            .ToArray();

    private static byte[] FileKeyBytes() =>
        Enumerable.Range(0, AesGcmStreamCipher.KeySize)
            .Select(index => (byte)(0xa0 + index))
            .ToArray();

    private static byte[] FileKeyNonceBytes() =>
        Enumerable.Range(0, AesGcmStreamCipher.NonceSize)
            .Select(index => (byte)(0x10 + index))
            .ToArray();

    private sealed class ScriptedRandomNumberGenerator(params byte[][] chunks)
        : RandomNumberGenerator
    {
        private readonly Queue<byte[]> _chunks = new(chunks.Select(chunk => chunk.ToArray()));

        public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());

        public override void GetBytes(Span<byte> data)
        {
            if (_chunks.Count == 0)
            {
                throw new InvalidOperationException("No scripted random bytes remain.");
            }

            var chunk = _chunks.Dequeue();
            if (chunk.Length != data.Length)
            {
                throw new InvalidOperationException(
                    $"Expected RNG request for {chunk.Length} bytes, got {data.Length}.");
            }

            chunk.CopyTo(data);
        }
    }
}
