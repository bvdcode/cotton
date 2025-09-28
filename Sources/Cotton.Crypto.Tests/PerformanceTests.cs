using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    [NonParallelizable]
    public class PerformanceTests
    {
        private static readonly Lock _initLock = new();
        private static byte[]? _sharedData;
        private static byte[]? _masterKey;
        private const int OneMb = 1024 * 1024;
        private const int TestDataSizeMb = 1000; // 1 GB
        private const int Iterations = 10;

        [SetUp]
        public void SetUp()
        {
            if (_sharedData != null && _masterKey != null) return;

            lock (_initLock)
            {
                if (_sharedData != null && _masterKey != null) return;

                // Fixed master key once for all tests
                _masterKey = new byte[32];
                for (int i = 0; i < _masterKey.Length; i++) _masterKey[i] = (byte)i;

                // Prepare shared plaintext buffer (1 GB)
                int sizeBytes = TestDataSizeMb * OneMb;
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
            Assert.Multiple(() =>
            {
                Assert.That(_sharedData, Is.Not.Null);
                Assert.That(_masterKey, Is.Not.Null);
            });

            byte[] source = _sharedData!;
            byte[] masterKey = _masterKey!;

            TestContext.Out.WriteLine("=== ENCRYPTION PERFORMANCE ===");

            // Warm-up (not measured)
            {
                using MemoryStream warmInput = new(source, 0, TestDataSizeMb * OneMb, writable: false, publiclyVisible: true);
                using MemoryStream warmEncrypted = new();
                AesGcmStreamCipher warmCipher = new(masterKey);
                await warmCipher.EncryptAsync(warmInput, warmEncrypted);
            }

            List<double> throughputs = [];

            for (int i = 0; i < Iterations; i++)
            {
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
            Assert.Multiple(() =>
            {
                Assert.That(_sharedData, Is.Not.Null);
                Assert.That(_masterKey, Is.Not.Null);
            });

            byte[] source = _sharedData!;
            byte[] masterKey = _masterKey!;
            int totalBytes = TestDataSizeMb * OneMb;

            // Prepare one ciphertext with the fixed key to reuse between iterations (not measured)
            byte[] encryptedPayload;
            {
                AesGcmStreamCipher cipher = new(masterKey);
                using MemoryStream input = new(source, 0, totalBytes, writable: false, publiclyVisible: true);
                using MemoryStream encrypted = new(capacity: totalBytes + 4096);
                await cipher.EncryptAsync(input, encrypted);
                encryptedPayload = encrypted.ToArray();
            }

            // Warm-up decrypt (not measured)
            {
                AesGcmStreamCipher warmCipher = new(masterKey);
                using MemoryStream warmEncrypted = new(encryptedPayload, writable: false);
                using MemoryStream warmDecrypted = new(capacity: totalBytes);
                await warmCipher.DecryptAsync(warmEncrypted, warmDecrypted);
            }

            TestContext.Out.WriteLine("=== DECRYPTION PERFORMANCE ===");

            List<double> throughputs = [];

            for (int i = 0; i < Iterations; i++)
            {
                AesGcmStreamCipher cipher = new(masterKey);

                using MemoryStream encryptedStream = new(encryptedPayload, writable: false);
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
