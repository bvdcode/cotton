using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class PerformanceTests
    {
        private static byte[]? _preGeneratedData;
        private static readonly object _lockObject = new object();

        [SetUp]
        public void Setup()
        {
            if (_preGeneratedData == null)
            {
                lock (_lockObject)
                {
                    if (_preGeneratedData == null)
                    {
                        TestContext.Out.WriteLine("Generating test data, please wait...");
                        Stopwatch sw = Stopwatch.StartNew();
                        
                        _preGeneratedData = new byte[1024 * 1024 * 1024]; // 1GB pre-generated

                        // Use a simple pattern instead of random data for speed
                        Parallel.For(0, _preGeneratedData.Length / 1024, chunk =>
                        {
                            int startIndex = chunk * 1024;
                            int endIndex = Math.Min(startIndex + 1024, _preGeneratedData.Length);
                            
                            for (int i = startIndex; i < endIndex; i++)
                            {
                                _preGeneratedData[i] = (byte)(i % 256);
                            }
                        });
                        
                        sw.Stop();
                        TestContext.Out.WriteLine($"Test data generated in {sw.ElapsedMilliseconds} ms");
                    }
                }
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public async Task EncryptDecrypt_PerformanceTest(int mbSize)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            int totalBytes = 1024 * 1024 * mbSize;

            using MemoryStream plainTextStream = new MemoryStream();
            plainTextStream.Write(_preGeneratedData.AsSpan(0, totalBytes));
            plainTextStream.Seek(0, SeekOrigin.Begin);

            using MemoryStream encryptedStream = new MemoryStream();
            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(plainTextStream, encryptedStream);
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

            Assert.That(decryptedStream.Length, Is.EqualTo(plainTextStream.Length));
            Assert.That(decryptedStream.ToArray().AsSpan(0, totalBytes).ToArray(),
                       Is.EqualTo(_preGeneratedData.AsSpan(0, totalBytes).ToArray()));

            // Performance should be consistent regardless of file size
            double throughputMBps = (double)(mbSize * 2) / totalTime * 1000;
            Assert.That(throughputMBps, Is.GreaterThan(1000), $"Throughput {throughputMBps:F1} MB/s is below minimum requirement of 1000 MB/s");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(1024)]
        public async Task EncryptDecrypt_SmallFiles_ShouldWork(int bytes)
        {
            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            byte[] testData = bytes > 0 ? _preGeneratedData.AsSpan(0, bytes).ToArray() : [];

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
                    byte[] testData = _preGeneratedData.AsSpan(0, 1024 * 1024).ToArray();

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
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10000));
        }

        [Test]
        public async Task Throughput_1GB_ShouldBeAtLeast_1GBPerSecond()
        {
            await EncryptDecrypt_PerformanceTest(1000);
        }
    }
}
