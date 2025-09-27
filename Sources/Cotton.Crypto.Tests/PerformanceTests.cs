using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class PerformanceTests
    {
        private static readonly byte[] PreGeneratedData;

        static PerformanceTests()
        {
            // Pre-generate data once for all tests to avoid RNG overhead
            PreGeneratedData = new byte[1024 * 1024 * 100]; // 100MB pre-generated

            // Use a simple pattern instead of random data for speed
            for (int i = 0; i < PreGeneratedData.Length; i++)
            {
                PreGeneratedData[i] = (byte)(i % 256);
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public async Task EncryptDecrypt_PerformanceTest(int mbSize)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new(masterKey);

            int totalBytes = 1024 * 1024 * mbSize;

            // Use pre-generated data to avoid RNG overhead
            using var plainTextStream = new MemoryStream();
            plainTextStream.Write(PreGeneratedData.AsSpan(0, totalBytes));
            plainTextStream.Seek(0, SeekOrigin.Begin);

            // Encrypt
            using var encryptedStream = new MemoryStream();
            var sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(plainTextStream, encryptedStream);
            var encryptTime = sw.ElapsedMilliseconds;

            // Decrypt
            encryptedStream.Seek(0, SeekOrigin.Begin);
            using var decryptedStream = new MemoryStream();
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            var decryptTime = sw.ElapsedMilliseconds;

            int totalTime = Math.Max(1, (int)(encryptTime + decryptTime)); // Ensure at least 1ms for calculation

            TestContext.Out.WriteLine($"Encryption of {mbSize} MB took {encryptTime} ms");
            TestContext.Out.WriteLine($"Decryption of {mbSize} MB took {decryptTime} ms");
            TestContext.Out.WriteLine($"Total time: {totalTime} ms");
            TestContext.Out.WriteLine($"Throughput: {(double)mbSize * 2 / totalTime * 1000:F1} MB/s");

            // Validate correctness
            Assert.That(decryptedStream.Length, Is.EqualTo(plainTextStream.Length));
            Assert.That(decryptedStream.ToArray().AsSpan(0, totalBytes).ToArray(),
                       Is.EqualTo(PreGeneratedData.AsSpan(0, totalBytes).ToArray()));

            // Performance assertions - should be very fast
            double throughputMBps = (double)(mbSize * 2) / totalTime * 1000;

            // Assert minimum performance requirements
            switch (mbSize)
            {
                case 1:
                    Assert.That(throughputMBps, Is.GreaterThan(100), "1MB should achieve >100 MB/s");
                    break;
                case 10:
                    Assert.That(throughputMBps, Is.GreaterThan(500), "10MB should achieve >500 MB/s");
                    break;
                case 100:
                    Assert.That(throughputMBps, Is.GreaterThan(1000), "100MB should achieve >1000 MB/s");
                    break;
            }
        }

        [TestCase(0)] // Empty file
        [TestCase(1)] // 1 byte
        [TestCase(100)] // 100 bytes
        [TestCase(1024)] // 1 KB
        public async Task EncryptDecrypt_SmallFiles_ShouldWork(int bytes)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new(masterKey);

            byte[] testData = bytes > 0 ? PreGeneratedData.AsSpan(0, bytes).ToArray() : [];

            using var plainTextStream = new MemoryStream(testData);
            using var encryptedStream = new MemoryStream();
            using var decryptedStream = new MemoryStream();

            // Encrypt
            await cipher.EncryptAsync(plainTextStream, encryptedStream);

            // Should have some encrypted data even for 0 bytes (headers)
            Assert.That(encryptedStream.Length, Is.GreaterThan(0));

            // Decrypt
            encryptedStream.Seek(0, SeekOrigin.Begin);
            await cipher.DecryptAsync(encryptedStream, decryptedStream);

            // Validate
            Assert.That(decryptedStream.Length, Is.EqualTo(testData.Length));
            if (bytes > 0)
            {
                Assert.That(decryptedStream.ToArray(), Is.EqualTo(testData));
            }
        }

        [Test]
        public async Task EncryptDecrypt_ConcurrentOperations_ShouldWork()
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);

            // Test concurrent operations with different ciphers
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var cipher = new AesGcmStreamCipher(masterKey, keyId: i + 1);
                    var testData = PreGeneratedData.AsSpan(0, 1024 * 1024).ToArray(); // 1MB each

                    using var plainTextStream = new MemoryStream(testData);
                    using var encryptedStream = new MemoryStream();
                    using var decryptedStream = new MemoryStream();

                    await cipher.EncryptAsync(plainTextStream, encryptedStream);

                    encryptedStream.Seek(0, SeekOrigin.Begin);
                    await cipher.DecryptAsync(encryptedStream, decryptedStream);

                    Assert.That(decryptedStream.ToArray(), Is.EqualTo(testData));
                }));
            }

            var sw = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            sw.Stop();

            TestContext.Out.WriteLine($"10 concurrent 1MB operations took {sw.ElapsedMilliseconds} ms");
            // Should complete in reasonable time even on slow hardware
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10000)); // 10 seconds max
        }

        [Test]
        public async Task Throughput_ShouldBeAtLeast_100MBPerSecond()
        {
            // This test ensures minimum acceptable performance
            // Run a simple 10MB test and check throughput
            await EncryptDecrypt_PerformanceTest(10);

            // The performance assertion is already done in EncryptDecrypt_PerformanceTest
        }
    }
}
