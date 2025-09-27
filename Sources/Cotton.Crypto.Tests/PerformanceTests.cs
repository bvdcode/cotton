using System.Diagnostics;
using Cotton.Crypto.Helpers;

namespace Cotton.Crypto.Tests
{
    public class PerformanceTests
    {
        private static readonly object _initLock = new object();
        private static byte[]? _sharedData; // 1 GB, заполняется в SetUp один раз
        private const int OneMb = 1024 * 1024;

        [SetUp]
        public void SetUp()
        {
            if (_sharedData != null)
            {
                return;
            }

            lock (_initLock)
            {
                if (_sharedData != null)
                {
                    return;
                }

                // Подготовка данных вне времени тестов
                int sizeBytes = 1000 * OneMb; // 1 GB
                byte[] data = new byte[sizeBytes];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(i & 0xFF);
                }
                _sharedData = data;
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public async Task EncryptDecrypt_PerformanceTest(int mbSize)
        {
            Assert.That(_sharedData, Is.Not.Null, "Test data must be initialized in SetUp");
            byte[] source = _sharedData!;

            int totalBytes = mbSize * OneMb;
            // Создаём поток поверх общего буфера без копирования
            using MemoryStream inputStream = new MemoryStream(source, 0, totalBytes, writable: false, publiclyVisible: true);
            using MemoryStream encryptedStream = new MemoryStream(capacity: totalBytes + 4096);

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

            Stopwatch sw = Stopwatch.StartNew();
            await cipher.EncryptAsync(inputStream, encryptedStream).ConfigureAwait(false);
            long encryptTime = sw.ElapsedMilliseconds;

            encryptedStream.Seek(0, SeekOrigin.Begin);
            using MemoryStream decryptedStream = new MemoryStream(capacity: totalBytes);
            sw.Restart();
            await cipher.DecryptAsync(encryptedStream, decryptedStream).ConfigureAwait(false);
            long decryptTime = sw.ElapsedMilliseconds;

            long totalTime = Math.Max(1, encryptTime + decryptTime);

            TestContext.Out.WriteLine($"Encryption of {mbSize} MB took {encryptTime} ms");
            TestContext.Out.WriteLine($"Decryption of {mbSize} MB took {decryptTime} ms");
            TestContext.Out.WriteLine($"Total time: {totalTime} ms");
            TestContext.Out.WriteLine($"Throughput: {(double)mbSize * 2 / totalTime * 1000:F1} MB/s");

            // Проверяем корректность
            Assert.That(decryptedStream.Length, Is.EqualTo(totalBytes));
            byte[] decrypted = decryptedStream.ToArray();
            for (int i = 0; i < totalBytes; i++)
            {
                if (decrypted[i] != source[i])
                {
                    Assert.Fail($"Decrypted data mismatch at index {i}");
                }
            }

            // Порог производительности. Для малых объёмов допускаем бОльшие накладные расходы.
            double throughputMBps = (double)(mbSize * 2) / totalTime * 1000;
            double minThroughput = mbSize switch
            {
                1 => 500,   // 1MB — допускаем > 500 MB/s из‑за накладных расходов
                10 => 1000, // 10MB — > 1 GB/s
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
                ? new MemoryStream(Array.Empty<byte>())
                : new MemoryStream(source, 0, bytes, writable: false, publiclyVisible: true);

            using MemoryStream encryptedStream = new MemoryStream();
            using MemoryStream decryptedStream = new MemoryStream();

            byte[] masterKey = RandomHelpers.GetRandomBytes(32);
            AesGcmStreamCipher cipher = new AesGcmStreamCipher(masterKey);

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
