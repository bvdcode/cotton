using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class QuickPerformanceCheck
    {
        [Test]
        public async Task RawPerformance_100MB_ShouldBeFast()
        {
            // Generate 100MB of simple data quickly
            byte[] testData = new byte[100 * 1024 * 1024];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i & 0xFF);
            }

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            using MemoryStream input = new MemoryStream(testData);
            using MemoryStream encrypted = new MemoryStream();
            using MemoryStream decrypted = new MemoryStream();

            // Measure encryption
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(input, encrypted);
            long encryptMs = sw.ElapsedMilliseconds;
            
            encrypted.Seek(0, SeekOrigin.Begin);
            sw.Restart();
            await cipher.DecryptAsync(encrypted, decrypted);
            long decryptMs = sw.ElapsedMilliseconds;

            TestContext.Out.WriteLine($"100MB Encrypt: {encryptMs}ms");
            TestContext.Out.WriteLine($"100MB Decrypt: {decryptMs}ms");
            TestContext.Out.WriteLine($"Total: {encryptMs + decryptMs}ms");
            TestContext.Out.WriteLine($"Speed: {200.0 / (encryptMs + decryptMs) * 1000:F1} MB/s");

            // Should be very fast
            Assert.That(encryptMs + decryptMs, Is.LessThan(1000), "100MB should process in under 1 second");
        }
    }
}