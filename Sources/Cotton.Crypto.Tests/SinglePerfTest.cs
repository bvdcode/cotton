using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class SinglePerfTest
    {
        private static readonly byte[] TestData = new byte[100 * 1024 * 1024];
        
        static SinglePerfTest()
        {
            // Initialize once
            for (int i = 0; i < TestData.Length; i++)
            {
                TestData[i] = (byte)(i & 0xFF);
            }
        }

        [Test]
        public async Task Single_100MB_Test()
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            using MemoryStream inputStream = new MemoryStream(TestData);
            using MemoryStream encryptedStream = new MemoryStream();
            
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(inputStream, encryptedStream);
            long encryptTime = sw.ElapsedMilliseconds;

            encryptedStream.Seek(0, SeekOrigin.Begin);
            using MemoryStream decryptedStream = new MemoryStream();
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            long decryptTime = sw.ElapsedMilliseconds;

            TestContext.Out.WriteLine($"100MB Encrypt: {encryptTime}ms");
            TestContext.Out.WriteLine($"100MB Decrypt: {decryptTime}ms");
            TestContext.Out.WriteLine($"Total: {encryptTime + decryptTime}ms");
            TestContext.Out.WriteLine($"Speed: {200.0 / (encryptTime + decryptTime) * 1000:F1} MB/s");

            Assert.That(decryptedStream.Length, Is.EqualTo(TestData.Length));
            Assert.That(encryptTime + decryptTime, Is.LessThan(1000));
        }
    }
}