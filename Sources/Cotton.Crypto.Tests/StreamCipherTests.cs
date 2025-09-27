using Cotton.Crypto.Abstractions;

namespace Cotton.Crypto.Tests
{
    public class StreamCipherTests
    {
        private readonly byte[] _masterKey = new byte[32];
        private readonly MemoryStream _plainTextStream = new();

        [SetUp]
        public void Setup()
        {
            Random.Shared.NextBytes(_masterKey);
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
    }
}
