using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    [NonParallelizable]
    public class PerformanceTests
    {
        private static readonly Lock _initLock = new();
        private static byte[]? _sharedData;
        private const int OneMb = 1024 * 1024;
        private const int TestDataSizeMb = 1000; // 1 GB
        private const int Iterations = 5;

        [SetUp]
        public void SetUp()
        {
            if (_sharedData != null) return;

            lock (_initLock)
            {
                if (_sharedData != null) return;

                int sizeBytes = TestDataSizeMb * OneMb; // 1 GB
                byte[] data = new byte[sizeBytes];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(i & 0xFF);
                }
                _sharedData = data;
            }
        }

        [Test]
        public async Task Encrypt_PerformanceTest()
        {
            Assert.That(_sharedData, Is.Not.Null);
            byte[] source = _sharedData!;

            // Edge cases inside main test
            foreach (int bytes in new[] { 0, 1 })
            {
                byte[] masterKeyEdge = RandomHelpers.GetRandomBytes(32);
                AesGcmStreamCipher cipherEdge = new(masterKeyEdge);

                using MemoryStream inputStream = bytes == 0
                    ? new MemoryStream([])
                    : new MemoryStream(source, 0, bytes, writable: false, publiclyVisible: true);
                using MemoryStream encryptedStream = new();
                using MemoryStream decryptedStream = new();

                await cipherEdge.EncryptAsync(inputStream, encryptedStream);
                encryptedStream.Position = 0;
                await cipherEdge.DecryptAsync(encryptedStream, decryptedStream);

                Assert.That(decryptedStream.Length, Is.EqualTo(bytes));
                if (bytes > 0)
                {
                    byte[] decrypted = decryptedStream.ToArray();
                    for (int i = 0; i < bytes; i++)
                    {
                        if (decrypted[i] != source[i])
                        {
                            Assert.Fail($"Decrypted data mismatch at index {i}");
                        }
                    }
                }
            }

            TestContext.Out.WriteLine("=== ENCRYPTION PERFORMANCE ===");
            List<double> throughputs = [];

            for (int i = 0; i < Iterations; i++)
            {
                byte[] masterKey = RandomHelpers.GetRandomBytes(32);
                AesGcmStreamCipher cipher = new(masterKey);

                int totalBytes = TestDataSizeMb * OneMb;
                using MemoryStream inputStream = new(source, 0, totalBytes, writable: false, publiclyVisible: true);
                using MemoryStream encryptedStream = new(capacity: totalBytes + 4096);

                long t0 = Stopwatch.GetTimestamp();
                await cipher.EncryptAsync(inputStream, encryptedStream);
                long t1 = Stopwatch.GetTimestamp();

                double timeSeconds = (t1 - t0) / (double)Stopwatch.Frequency;
                double throughputMBps = TestDataSizeMb / timeSeconds;
                throughputs.Add(throughputMBps);

                TestContext.Out.WriteLine($"Run {i + 1}: {throughputMBps:F1} MB/s");
            }

            double avgThroughput = throughputs.Average();
            TestContext.Out.WriteLine($"Average Encryption: {avgThroughput:F1} MB/s");
        }

        [Test]
        public async Task Decrypt_PerformanceTest()
        {
            Assert.That(_sharedData, Is.Not.Null);
            byte[] source = _sharedData!;

            // Edge cases inside main test
            foreach (int bytes in new[] { 0, 1 })
            {
                byte[] masterKeyEdge = RandomHelpers.GetRandomBytes(32);
                AesGcmStreamCipher cipherEdge = new(masterKeyEdge);

                using MemoryStream inputStream = bytes == 0
                    ? new MemoryStream([])
                    : new MemoryStream(source, 0, bytes, writable: false, publiclyVisible: true);
                using MemoryStream encryptedStream = new();
                using MemoryStream decryptedStream = new();

                await cipherEdge.EncryptAsync(inputStream, encryptedStream);
                encryptedStream.Position = 0;
                await cipherEdge.DecryptAsync(encryptedStream, decryptedStream);

                Assert.That(decryptedStream.Length, Is.EqualTo(bytes));
                if (bytes > 0)
                {
                    byte[] decrypted = decryptedStream.ToArray();
                    for (int i = 0; i < bytes; i++)
                    {
                        if (decrypted[i] != source[i])
                        {
                            Assert.Fail($"Decrypted data mismatch at index {i}");
                        }
                    }
                }
            }

            TestContext.Out.WriteLine("=== DECRYPTION PERFORMANCE ===");
            List<double> throughputs = [];

            for (int i = 0; i < Iterations; i++)
            {
                byte[] masterKey = RandomHelpers.GetRandomBytes(32);
                AesGcmStreamCipher cipher = new(masterKey);

                int totalBytes = TestDataSizeMb * OneMb;

                using var sourceStream = new MemoryStream(source, 0, totalBytes, writable: false, publiclyVisible: true);
                using var encryptedStream = new MemoryStream(capacity: totalBytes + 4096);
                await cipher.EncryptAsync(sourceStream, encryptedStream);

                encryptedStream.Position = 0;
                using MemoryStream decryptedStream = new(capacity: totalBytes);

                long t0 = Stopwatch.GetTimestamp();
                await cipher.DecryptAsync(encryptedStream, decryptedStream);
                long t1 = Stopwatch.GetTimestamp();

                double timeSeconds = (t1 - t0) / (double)Stopwatch.Frequency;
                double throughputMBps = TestDataSizeMb / timeSeconds;
                throughputs.Add(throughputMBps);

                TestContext.Out.WriteLine($"Run {i + 1}: {throughputMBps:F1} MB/s");
            }

            double avgThroughput = throughputs.Average();
            TestContext.Out.WriteLine($"Average Decryption: {avgThroughput:F1} MB/s");
        }
    }
}
