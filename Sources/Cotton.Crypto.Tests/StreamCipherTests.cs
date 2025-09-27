using Cotton.Crypto.Models;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class StreamCipherTests
    {
        private readonly byte[] _masterKey = RandomHelpers.GetRandomBytes(32);
        private readonly MemoryStream _plainTextStream = new();

        [SetUp]
        public void Setup()
        {
            byte[] plainText = new byte[1024 * 1024];
            Random.Shared.NextBytes(plainText);
            _plainTextStream.Write(plainText, 0, plainText.Length);
            _plainTextStream.Seek(default, SeekOrigin.Begin);
        }

        [Test]
        public async Task EncryptStream_ValidParameters_ShouldDecryptSuccessfully()
        {
            AesGcmStreamCipher cipher = new(_masterKey);
            using MemoryStream encryptedStream = new();
            using MemoryStream decryptedStream = new();
            await cipher.EncryptAsync(_plainTextStream, encryptedStream);
            encryptedStream.Seek(default, SeekOrigin.Begin);
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            decryptedStream.Seek(default, SeekOrigin.Begin);
            Assert.That(decryptedStream.Length, Is.EqualTo(_plainTextStream.Length));
            Assert.That(decryptedStream.ToArray(), Is.EqualTo(_plainTextStream.ToArray()));
        }

        [Test]
        public void EncryptStream_ValidParameters_ShouldParseAndValidateHeader()
        {
            AesGcmKeyHeader expectedHeader = new(123, [1, 2, 3], [4, 5, 6], [7, 8, 9], 12345);
            ReadOnlyMemory<byte> headerBytes = expectedHeader.ToBytes();
            using MemoryStream ms = new(headerBytes.ToArray());
            AesGcmKeyHeader parsedHeader = AesGcmKeyHeader.FromStream(ms, 3, 3);
            Assert.Multiple(() =>
            {
                Assert.That(parsedHeader.KeyId, Is.EqualTo(expectedHeader.KeyId));
                Assert.That(parsedHeader.Nonce, Is.EqualTo(expectedHeader.Nonce));
                Assert.That(parsedHeader.Tag, Is.EqualTo(expectedHeader.Tag));
                Assert.That(parsedHeader.EncryptedKey, Is.EqualTo(expectedHeader.EncryptedKey));
                Assert.That(parsedHeader.dataLength, Is.EqualTo(expectedHeader.dataLength));
            });
        }
    }
}
