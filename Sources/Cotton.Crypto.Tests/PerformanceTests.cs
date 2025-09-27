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
            // Generate data on demand, not pre-allocate massive arrays
            byte[] testData = new byte[1024 * 1024 * mbSize];
            
            // Fast pattern generation
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i & 0xFF);
            }

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            using MemoryStream inputStream = new MemoryStream(testData);
            using MemoryStream encryptedStream = new MemoryStream();
            
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(inputStream, encryptedStream);
            long encryptTime = sw.ElapsedMilliseconds;

            encryptedStream.Seek(0, SeekOrigin.Begin);
            using MemoryStream decryptedStream = new MemoryStream();
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            long decryptTime = sw.ElapsedMilliseconds;

            long totalTime = Math.Max(1, encryptTime + decryptTime);

            TestContext.Out.WriteLine($"Encryption of {mbSize} MB took {encryptTime} ms");
            TestContext.Out.WriteLine($"Decryption of {mbSize} MB took {decryptTime} ms");
            TestContext.Out.WriteLine($"Total time: {totalTime} ms");
            TestContext.Out.WriteLine($"Throughput: {(double)mbSize * 2 / totalTime * 1000:F1} MB/s");

            Assert.That(decryptedStream.Length, Is.EqualTo(testData.Length));
            Assert.That(decryptedStream.ToArray(), Is.EqualTo(testData));

            double throughputMBps = (double)(mbSize * 2) / totalTime * 1000;
            
            // Different performance requirements based on size due to overhead
            double minThroughput = mbSize switch
            {
                1 => 200,    // 1MB: 200 MB/s minimum (overhead is significant)
                10 => 800,   // 10MB: 800 MB/s minimum  
                100 => 1500, // 100MB: 1500 MB/s minimum
                _ => 1000    // Default: 1000 MB/s
            };
            
            Assert.That(throughputMBps, Is.GreaterThan(minThroughput), 
                       $"Throughput {throughputMBps:F1} MB/s is below minimum requirement of {minThroughput} MB/s for {mbSize}MB");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(1024)]
        public async Task EncryptDecrypt_SmallFiles_ShouldWork(int bytes)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            byte[] testData = new byte[bytes];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i & 0xFF);
            }

            using MemoryStream plainTextStream = new MemoryStream(testData);
            using MemoryStream encryptedStream = new MemoryStream();
            using MemoryStream decryptedStream = new MemoryStream();

            await cipher.EncryptAsync(plainTextStream, encryptedStream);

            Assert.That(encryptedStream.Length, Is.GreaterThan(0));

            encryptedStream.Seek(0, SeekOrigin.Begin);
            await cipher.DecryptAsync(encryptedStream, decryptedStream);

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

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey, keyId: i + 1);
                    
                    // 1MB test data per task
                    byte[] testData = new byte[1024 * 1024];
                    for (int j = 0; j < testData.Length; j++)
                    {
                        testData[j] = (byte)(j & 0xFF);
                    }

                    using MemoryStream plainTextStream = new MemoryStream(testData);
                    using MemoryStream encryptedStream = new MemoryStream();
                    using MemoryStream decryptedStream = new MemoryStream();

                    await cipher.EncryptAsync(plainTextStream, encryptedStream);

                    encryptedStream.Seek(0, SeekOrigin.Begin);
                    await cipher.DecryptAsync(encryptedStream, decryptedStream);

                    Assert.That(decryptedStream.ToArray(), Is.EqualTo(testData));
                }));
            }

            Stopwatch sw = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            sw.Stop();

            TestContext.Out.WriteLine($"10 concurrent 1MB operations took {sw.ElapsedMilliseconds} ms");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000));
        }

        [Test] 
        public async Task PerformanceTest_1GB_ShouldBeAtLeast_1GBPerSecond()
        {
            TestContext.Out.WriteLine("Testing 1GB performance...");
            
            // Generate 1GB data in chunks to avoid memory pressure
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            using MemoryStream largeData = new MemoryStream();
            
            // Write 1GB in 100MB chunks
            byte[] chunk = new byte[100 * 1024 * 1024]; // 100MB
            for (int i = 0; i < chunk.Length; i++)
            {
                chunk[i] = (byte)(i & 0xFF);
            }
            
            for (int i = 0; i < 10; i++) // 10 * 100MB = 1GB
            {
                largeData.Write(chunk);
            }
            
            largeData.Seek(0, SeekOrigin.Begin);

            using MemoryStream encryptedStream = new MemoryStream();
            
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(largeData, encryptedStream);
            long encryptTime = sw.ElapsedMilliseconds;

            encryptedStream.Seek(0, SeekOrigin.Begin);
            using MemoryStream decryptedStream = new MemoryStream();
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream);
            long decryptTime = sw.ElapsedMilliseconds;

            long totalTime = Math.Max(1, encryptTime + decryptTime);

            TestContext.Out.WriteLine($"1GB Encrypt: {encryptTime} ms");
            TestContext.Out.WriteLine($"1GB Decrypt: {decryptTime} ms");
            TestContext.Out.WriteLine($"Total time: {totalTime} ms");
            TestContext.Out.WriteLine($"Throughput: {2000.0 / totalTime * 1000:F1} MB/s");

            double throughputMBps = 2000.0 / totalTime * 1000;
            Assert.That(throughputMBps, Is.GreaterThan(1000), $"1GB throughput {throughputMBps:F1} MB/s should be at least 1000 MB/s");
        }
    }
}
