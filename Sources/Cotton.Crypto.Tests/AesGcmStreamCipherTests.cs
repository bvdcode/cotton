using System.Text;
using Cotton.Crypto.Models;
using System.Security.Cryptography;

namespace Cotton.Crypto.Tests;

public class AesGcmStreamCipherTests
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MinChunkSize = 65_536;
    private const int MaxChunkSize = 16_777_216;

    private static byte[] CreateRandomBytes(int size, int seed = 12345)
    {
        var rng = new Random(seed);
        var data = new byte[size];
        rng.NextBytes(data);
        return data;
    }

    private static AesGcmStreamCipher CreateCipher(byte[] masterKey, int keyId = 1)
        => new(masterKey, keyId);

    private static async Task<MemoryStream> EncryptToMemoryAsync(AesGcmStreamCipher cipher, Stream input, int chunkSize)
    {
        var output = new MemoryStream();
        await cipher.EncryptAsync(input, output, chunkSize);
        output.Position = 0;
        return output;
    }

    private static async Task<MemoryStream> DecryptToMemoryAsync(AesGcmStreamCipher cipher, Stream input)
    {
        var output = new MemoryStream();
        await cipher.DecryptAsync(input, output);
        output.Position = 0;
        return output;
    }

    private static (AesGcmKeyHeader fileHeader, AesGcmKeyHeader? firstChunkHeader, int firstCiphertextOffset) ParseHeaders(byte[] encrypted)
    {
        using var ms = new MemoryStream(encrypted, writable: false);
        var fileHeader = AesGcmKeyHeader.FromStream(ms, NonceSize, TagSize);

        AesGcmKeyHeader? chunkHeader = null;
        int firstCiphertextOffset = (int)ms.Position;
        try
        {
            chunkHeader = AesGcmKeyHeader.FromStream(ms, NonceSize, TagSize);
            firstCiphertextOffset = (int)ms.Position;
        }
        catch (EndOfStreamException)
        {
            // no chunks
        }

        return (fileHeader, chunkHeader, firstCiphertextOffset);
    }

    private static byte[] ValidMasterKey()
    {
        // 32 bytes deterministic key
        return [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
    }

    [Test]
    public void Constructor_InvalidMasterKey_Throws()
    {
        var tooShort = new byte[16];
        Assert.Throws<ArgumentException>(() => CreateCipher(tooShort));
    }

    [Test]
    public void Constructor_InvalidKeyId_Throws()
    {
        var mk = ValidMasterKey();
        Assert.Throws<ArgumentOutOfRangeException>(() => new AesGcmStreamCipher(mk, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AesGcmStreamCipher(mk, -1));
    }

    [Test]
    public void Encrypt_InvalidChunkSize_Throws_BelowMin()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk);
        using var input = new MemoryStream(CreateRandomBytes(1024));
        using var output = new MemoryStream();
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await cipher.EncryptAsync(input, output, MinChunkSize - 1));
    }

    [Test]
    public void Encrypt_InvalidChunkSize_Throws_AboveMax()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk);
        using var input = new MemoryStream(CreateRandomBytes(1024));
        using var output = new MemoryStream();
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await cipher.EncryptAsync(input, output, MaxChunkSize + 1));
    }

    [Test]
    public async Task RoundTrip_SmallData_DefaultChunk()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk, keyId: 7);

        var plaintext = Encoding.UTF8.GetBytes("Hello AES-GCM streaming!");
        using var input = new MemoryStream(plaintext);
        var encrypted = await EncryptToMemoryAsync(cipher, input, chunkSize: 1_048_576);

        encrypted.Position = 0;
        var decrypted = await DecryptToMemoryAsync(cipher, encrypted);

        Assert.That(plaintext, Is.EqualTo(decrypted.ToArray()));
    }

    [TestCase(65_536)]
    [TestCase(131_072)]
    [TestCase(1_048_576)]
    public async Task RoundTrip_LargeData_CustomChunkSizes(int chunkSize)
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk, keyId: 3);

        int dataLen = (int)(chunkSize * 2.5) + 123; // guarantee multiple chunks
        var data = CreateRandomBytes(dataLen);
        using var input = new MemoryStream(data);

        var encrypted = await EncryptToMemoryAsync(cipher, input, chunkSize);

        encrypted.Position = 0;
        var decrypted = await DecryptToMemoryAsync(cipher, encrypted);

        Assert.That(data, Is.EqualTo(decrypted.ToArray()));
    }

    [Test]
    public async Task Encrypt_WritesHeader_WithKeyId_AndDataLength()
    {
        var mk = ValidMasterKey();
        int keyId = 42;
        var cipher = CreateCipher(mk, keyId);

        var data = CreateRandomBytes(200_000);
        using var input = new MemoryStream(data);
        var encrypted = await EncryptToMemoryAsync(cipher, input, chunkSize: 65_536);

        var bytes = encrypted.ToArray();
        var (fileHeader, firstChunkHeader, _) = ParseHeaders(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(fileHeader.KeyId, Is.EqualTo(keyId));
            Assert.That(fileHeader.DataLength, Is.EqualTo(data.Length));
            Assert.That(fileHeader.Nonce, Has.Length.EqualTo(NonceSize));
            Assert.That(fileHeader.Tag, Has.Length.EqualTo(TagSize));
            Assert.That(firstChunkHeader, Is.Not.Null);
        });
        Assert.Multiple(() =>
        {
            Assert.That(firstChunkHeader!.Value.Nonce, Has.Length.EqualTo(NonceSize));
            Assert.That(firstChunkHeader!.Value.Tag, Has.Length.EqualTo(TagSize));
            Assert.That(firstChunkHeader!.Value.DataLength, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task Encrypt_WritesHeader_NonSeekable_DataLengthZero()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk, keyId: 9);

        var data = CreateRandomBytes(100_000);
        using var inner = new MemoryStream(data);
        using var nonSeek = new NonSeekableReadStream(inner);

        var encrypted = new MemoryStream();
        await cipher.EncryptAsync(nonSeek, encrypted, chunkSize: 65_536);
        var bytes = encrypted.ToArray();

        var (fileHeader, _, _) = ParseHeaders(bytes);
        Assert.That(fileHeader.DataLength, Is.EqualTo(0));
    }

    [Test]
    public void Decrypt_Fails_OnTamperedCiphertext()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk);

        var data = CreateRandomBytes(120_000);
        using var input = new MemoryStream(data);
        var encrypted = EncryptToMemoryAsync(cipher, input, chunkSize: 65_536).GetAwaiter().GetResult();

        var bytes = encrypted.ToArray();
        var (_, firstChunkHeader, firstCipherOffset) = ParseHeaders(bytes);
        Assert.That(firstChunkHeader, Is.Not.Null, "Expected at least one chunk.");

        // Flip one byte in the first chunk ciphertext
        if (firstChunkHeader!.Value.DataLength > 0)
        {
            bytes[firstCipherOffset] ^= 0xFF;
        }

        using var tampered = new MemoryStream(bytes, writable: false);
        using var output = new MemoryStream();
        Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () => await cipher.DecryptAsync(tampered, output));
    }

    [Test]
    public void Encrypt_Cancellation_Throws()
    {
        var mk = ValidMasterKey();
        var cipher = CreateCipher(mk);

        var data = CreateRandomBytes(1_000_000);
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () => await cipher.EncryptAsync(input, output, chunkSize: 65_536, ct: cts.Token));
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // Do not dispose inner to mimic typical stream wrappers unless needed
        }
    }
}

public class AesGcmStreamCipherTests_EdgesAndCorrectness
{
    private static readonly byte[] MasterKey = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    [TestCase(0)]
    [TestCase(1)]
    public async Task EncryptDecrypt_EdgeCases_ShouldWork(int bytes)
    {
        byte[] source = new byte[Math.Max(1, bytes)];
        for (int i = 0; i < bytes; i++) source[i] = (byte)i;

        using MemoryStream inputStream = bytes == 0 ? new MemoryStream([]) : new MemoryStream(source, 0, bytes, writable: false, publiclyVisible: true);
        using MemoryStream encryptedStream = new();
        using MemoryStream decryptedStream = new();

        var cipher = new AesGcmStreamCipher(MasterKey);

        await cipher.EncryptAsync(inputStream, encryptedStream);
        encryptedStream.Position = 0;
        await cipher.DecryptAsync(encryptedStream, decryptedStream);

        Assert.That(decryptedStream.Length, Is.EqualTo(bytes));
        if (bytes > 0)
        {
            Assert.That(decryptedStream.ToArray(), Is.EqualTo(source.AsSpan(0, bytes).ToArray()));
        }
    }

    [Test]
    public async Task EncryptDecrypt_Correctness_RoundTrip_ShouldMatch()
    {
        var text = string.Join(',', Enumerable.Range(0, 2000));
        var data = Encoding.UTF8.GetBytes(text);

        using var input = new MemoryStream(data);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        var cipher = new AesGcmStreamCipher(MasterKey);
        await cipher.EncryptAsync(input, encrypted);
        encrypted.Position = 0;
        await cipher.DecryptAsync(encrypted, decrypted);

        Assert.That(decrypted.ToArray(), Is.EqualTo(data));
    }
}