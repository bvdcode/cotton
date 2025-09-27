using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class PerformanceTests
    {
        private static readonly Lock _initLock = new();
        private static byte[]? _sharedData; // initialized in SetUp once
        private const int OneMb = 1024 * 1024;

        [SetUp]
        public void SetUp()
        {
            if (_sharedData != null) return;

            lock (_initLock)
            {
                if (_sharedData != null) return;

                int sizeBytes = 1000 * OneMb; // 1 GB
                byte[] data = new byte[sizeBytes];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(i & 0xFF);
                }
                _sharedData = data;
            }
        }

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public async Task EncryptDecrypt_PerformanceTest(int mbSize)
        {
            Assert.That(_sharedData, Is.Not.Null, "Test data must be initialized in SetUp");
            byte[] source = _sharedData!;

            int totalBytes = mbSize * OneMb;
            using MemoryStream inputStream = new(source, 0, totalBytes, writable: false, publiclyVisible: true);
            using MemoryStream encryptedStream = new(capacity: totalBytes + 4096);

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new(masterKey);

            long t0 = Stopwatch.GetTimestamp();
            await cipher.EncryptAsync(inputStream, encryptedStream).ConfigureAwait(false);
            long t1 = Stopwatch.GetTimestamp();

            encryptedStream.Position = 0;
            using MemoryStream decryptedStream = new(capacity: totalBytes);
            await cipher.DecryptAsync(encryptedStream, decryptedStream).ConfigureAwait(false);
            long t2 = Stopwatch.GetTimestamp();

            long encTicks = t1 - t0;
            long decTicks = t2 - t1;
            long totalTicks = t2 - t0;
            double tickFreq = Stopwatch.Frequency;
            double encMs = encTicks * 1000.0 / tickFreq;
            double decMs = decTicks * 1000.0 / tickFreq;
            double totalMs = totalTicks * 1000.0 / tickFreq;

            TestContext.Out.WriteLine($"Encryption of {mbSize} MB: {encMs:F2} ms");
            TestContext.Out.WriteLine($"Decryption of {mbSize} MB: {decMs:F2} ms");
            TestContext.Out.WriteLine($"Total: {totalMs:F2} ms");
            TestContext.Out.WriteLine($"Throughput: {(mbSize * 2) / (totalTicks / tickFreq):F1} MB/s");

            Assert.That(decryptedStream.Length, Is.EqualTo(totalBytes));
            byte[] decrypted = decryptedStream.ToArray();
            for (int i = 0; i < totalBytes; i++)
            {
                if (decrypted[i] != source[i])
                {
                    Assert.Fail($"Decrypted data mismatch at index {i}");
                }
            }

            double throughputMBps = (mbSize * 2) / (totalTicks / tickFreq);
            double minThroughput = mbSize switch
            {
                1 => 500,
                10 => 1000,
                100 => 1000,
                1000 => 1000,
                _ => 1000
            };
            Assert.That(throughputMBps, Is.GreaterThan(minThroughput),
                $"Throughput {throughputMBps:F1} MB/s is below minimum requirement of {minThroughput} MB/s for {mbSize}MB");
        }

        [TestCase(0)]
        [TestCase(1)]
        public async Task EncryptDecrypt_EdgeCases_ShouldWork(int bytes)
        {
            Assert.That(_sharedData, Is.Not.Null, "Test data must be initialized in SetUp");
            byte[] source = _sharedData!;

            using MemoryStream inputStream = bytes == 0
                ? new MemoryStream([])
                : new MemoryStream(source, 0, bytes, writable: false, publiclyVisible: true);

            using MemoryStream encryptedStream = new();
            using MemoryStream decryptedStream = new();

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new(masterKey);

            await cipher.EncryptAsync(inputStream, encryptedStream).ConfigureAwait(false);
            Assert.That(encryptedStream.Length, Is.GreaterThan(0));

            encryptedStream.Position = 0;
            await cipher.DecryptAsync(encryptedStream, decryptedStream).ConfigureAwait(false);

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
    }
}
