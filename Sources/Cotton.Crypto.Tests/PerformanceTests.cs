using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class PerformanceTests
    {
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        public async Task EncryptDecrypt_PerformanceTest(int mbSize)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new(masterKey);

            // Prepare a large plaintext stream
            using MemoryStream plainTextStream = new();
            byte[] plainText = new byte[1024 * 1024 * mbSize]; // mbSize MB
            Random.Shared.NextBytes(plainText);
            plainTextStream.Write(plainText, 0, plainText.Length);
            plainTextStream.Seek(default, SeekOrigin.Begin);

            // Encrypt
            using MemoryStream encryptedStream = new();
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(plainTextStream, encryptedStream);
            // use test output to print to console
            TestContext.Out.WriteLine($"Encryption of {mbSize} MB took {sw.ElapsedMilliseconds} ms");

            // Decrypt
            encryptedStream.Seek(default, SeekOrigin.Begin);
            using MemoryStream decryptedStream = new();
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            TestContext.Out.WriteLine($"Decryption of {mbSize} MB took {sw.ElapsedMilliseconds} ms");

            // Validate
            decryptedStream.Seek(default, SeekOrigin.Begin);
            Assert.That(decryptedStream.Length, Is.EqualTo(plainTextStream.Length));
            Assert.That(decryptedStream.ToArray(), Is.EqualTo(plainTextStream.ToArray()));
        }
    }
}
