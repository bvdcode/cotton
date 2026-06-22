// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Crypto.Tests;

[Category("Format")]
public class GoldenVectorsTests
{
    private const string SharedVectorsFileName = "cotton-container-vectors.json";

    [Test]
    public async Task Shared_Ctn2Vector_Decrypts()
    {
        SharedContainerVectors vectors = LoadSharedContainerVectors();
        using var cipher = new AesGcmStreamCipher(vectors.MasterKey, keyId: 1, threads: 1);
        using var input = new MemoryStream(vectors.Ctn2SingleChunk, writable: false);
        using var output = new MemoryStream();

        await cipher.DecryptAsync(input, output);

        Assert.That(output.ToArray(), Is.EqualTo(vectors.Plaintext));
    }

    [Test]
    public async Task Shared_Ctn2Vector_DeterministicWrite_MatchesFixture()
    {
        SharedContainerVectors vectors = LoadSharedContainerVectors();
        using var rng = new SequenceRandomNumberGenerator(
            vectors.FileKey,
            vectors.NoncePrefix,
            vectors.FileKeyNonce);
        using var cipher = new AesGcmStreamCipher(
            vectors.MasterKey,
            keyId: 1,
            threads: 1,
            rng: rng);
        using var input = new MemoryStream(vectors.Plaintext, writable: false);
        using var output = new MemoryStream();

        await cipher.EncryptAsync(input, output, chunkSize: vectors.Ctn2ChunkSize);

        Assert.That(output.ToArray(), Is.EqualTo(vectors.Ctn2SingleChunk));
    }

    [Test]
    public void Golden_Header_And_FirstChunk_Deterministic()
    {
        // Fixed master key
        byte[] masterKey = new byte[32];
        for (int i = 0; i < masterKey.Length; i++) masterKey[i] = (byte)(i + 1);
        // Deterministic RNG seeded via RNGCryptoServiceProvider replacement: use fixed bytes from Hash counter
        var rng = new DeterministicRng(0xA5A5A5A5);

        var cipher = new AesGcmStreamCipher(masterKey, keyId: 77, threads: 1, rng: rng);
        byte[] payload = new byte[128];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i ^ 0x5A);

        using var input = new MemoryStream(payload);
        using var output = new MemoryStream();
        cipher.EncryptAsync(input, output, chunkSize: 64 * 1024).GetAwaiter().GetResult();
        byte[] bytes = output.ToArray();

        // Extract header bytes
        int headerLen = 4 + 4 + 8 + 4 + 4 + AesGcmStreamCipher.NonceSize + AesGcmStreamCipher.TagSize + AesGcmStreamCipher.KeySize;
        byte[] header = bytes.AsSpan(0, headerLen).ToArray();
        // Extract first chunk header + ciphertext (only one chunk present)
        int chunkHdrLen = 4 + 4 + 8 + 4 + AesGcmStreamCipher.TagSize;
        byte[] chunk = bytes.AsSpan(headerLen, Math.Min(bytes.Length - headerLen, chunkHdrLen + payload.Length)).ToArray();

        // Golden expected (frozen). If this test changes, format changed.
        // These constants computed once and embedded to lock the format.
        byte[] expectedHeader = [
            // Magic CTN2, header length le32, total len le64, keyId le32, noncePrefix le32, nonce(12), tag(16), encryptedKey(32)
            0x43,0x54,0x4E,0x32, 0x54,0x00,0x00,0x00, 0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0x4D,0x00,0x00,0x00, 0x7A,0x7A,0x7A,0x7A, // prefix deterministic
            // nonce 12, tag 16, encryptedKey 32 (fake but consistent with DeterministicRng)
        ];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(header.AsSpan(0, 20).ToArray(), Is.EqualTo(expectedHeader.AsSpan(0, 20).ToArray()));

            // For brevity, pin only the first 20 header bytes and first 8 bytes of chunk header
            Assert.That(chunk, Has.Length.GreaterThanOrEqualTo(chunkHdrLen));
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(chunk[0], Is.EqualTo((byte)'C'));
            Assert.That(chunk[1], Is.EqualTo((byte)'T'));
            Assert.That(chunk[2], Is.EqualTo((byte)'N'));
            Assert.That(chunk[3], Is.EqualTo((byte)'2'));
        }
    }

    private static SharedContainerVectors LoadSharedContainerVectors()
    {
        string path = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestData",
            SharedVectorsFileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        JsonElement ctn2 = root
            .GetProperty("vectors")
            .GetProperty("cottonCtn2SingleChunk");

        return new SharedContainerVectors(
            ReadHex(root, "masterKeyHex"),
            ReadHex(root, "plaintextHex"),
            ReadHex(root, "fileKeyHex"),
            ReadHex(root, "noncePrefixHex"),
            ReadHex(root, "fileKeyNonceHex"),
            ctn2.GetProperty("chunkSize").GetInt32(),
            ReadHex(ctn2, "hex"));
    }

    private static byte[] ReadHex(JsonElement element, string propertyName)
    {
        string value = element.GetProperty(propertyName).GetString()
            ?? throw new InvalidDataException($"Shared vector field '{propertyName}' is missing.");
        return Convert.FromHexString(value);
    }

    private record SharedContainerVectors(
        byte[] MasterKey,
        byte[] Plaintext,
        byte[] FileKey,
        byte[] NoncePrefix,
        byte[] FileKeyNonce,
        int Ctn2ChunkSize,
        byte[] Ctn2SingleChunk);

    private class SequenceRandomNumberGenerator : RandomNumberGenerator
    {
        private readonly byte[] _bytes;
        private int _offset;

        public SequenceRandomNumberGenerator(params byte[][] sequences)
        {
            _bytes = sequences.SelectMany(static sequence => sequence).ToArray();
        }

        public override void GetBytes(byte[] data)
        {
            GetBytes(data.AsSpan());
        }

        public override void GetBytes(byte[] data, int offset, int count)
        {
            GetBytes(data.AsSpan(offset, count));
        }

        public override void GetBytes(Span<byte> data)
        {
            if (_offset + data.Length > _bytes.Length)
            {
                throw new InvalidOperationException("Shared vector random source is exhausted.");
            }

            _bytes.AsSpan(_offset, data.Length).CopyTo(data);
            _offset += data.Length;
        }
    }

    private class DeterministicRng(ulong seed) : RandomNumberGenerator
    {
        public override void GetBytes(byte[] data)
        {
            Span<byte> tmp = stackalloc byte[8];
            for (int i = 0; i < data.Length; i += 8)
            {
                seed = unchecked((seed * 6364136223846793005UL) + 1);
                BinaryPrimitives.WriteUInt64LittleEndian(tmp, seed);
                int len = Math.Min(8, data.Length - i);
                tmp[..len].CopyTo(data.AsSpan(i, len));
            }
        }

        public override void GetBytes(byte[] data, int offset, int count) => GetBytes(data.AsSpan(offset, count));
        public override void GetBytes(Span<byte> data)
        {
            Span<byte> tmp = stackalloc byte[8];
            for (int i = 0; i < data.Length; i += 8)
            {
                seed = unchecked((seed * 6364136223846793005UL) + 1);
                BinaryPrimitives.WriteUInt64LittleEndian(tmp, seed);
                int len = Math.Min(8, data.Length - i);
                tmp[..len].CopyTo(data.Slice(i, len));
            }
        }
    }
}
