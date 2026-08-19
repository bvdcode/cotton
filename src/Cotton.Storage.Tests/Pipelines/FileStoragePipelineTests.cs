// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace Cotton.Storage.Tests.Pipelines
{
    [TestFixture]
    public class FileStoragePipelineTests
    {
        private class FakeStorageBackend : IStorageBackend
        {
            private readonly Dictionary<string, byte[]> _storage = [];

            public void CleanupTempFiles(TimeSpan ttl)
            {
                // No-op for in-memory backend
            }

            public Task<bool> DeleteAsync(string uid)
            {
                return Task.FromResult(_storage.Remove(uid));
            }

            public Task<bool> ExistsAsync(string uid)
            {
                return Task.FromResult(_storage.ContainsKey(uid));
            }

            public Task<long> GetSizeAsync(string uid)
            {
                return Task.FromResult(_storage.TryGetValue(uid, out byte[]? data) ? data.Length : 0L);
            }

            public Task<Stream> ReadAsync(string uid)
            {
                if (!_storage.TryGetValue(uid, out byte[]? data))
                {
                    throw new FileNotFoundException($"UID not found: {uid}");
                }
                return Task.FromResult<Stream>(new MemoryStream(data));
            }

            public Task<long> WriteAsync(
                string uid,
                Stream stream)
            {
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] stored = ms.ToArray();
                _storage[uid] = stored;
                return Task.FromResult(stored.LongLength);
            }

            public async IAsyncEnumerable<string> ListAllKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
            {
                foreach (string key in _storage.Keys)
                {
                    yield return key;
                }
                await Task.CompletedTask;
            }
        }

        private class FakeBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            public IStorageBackend GetBackend() => backend;
        }

        private class MarkerProcessor(int priority, byte marker) : IStorageProcessor
        {
            public int Priority => priority;

            public async Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
            {
                MemoryStream ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] data = ms.ToArray();

                // Remove marker from end
                if (data.Length > 0 && data[^1] == marker)
                {
                    return new MemoryStream(data[..^1]) { Position = 0 };
                }

                return new MemoryStream(data) { Position = 0 };
            }

            public async Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
            {
                MemoryStream ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                _ = ms.ToArray();

                // Add marker to end
                ms.WriteByte(marker);
                ms.Position = 0;
                return ms;
            }
        }

        private class CountingProcessor : IStorageProcessor
        {
            public int Priority => 100;
            public int WriteCalls;

            public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
            {
                return Task.FromResult(stream);
            }

            public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
            {
                System.Threading.Interlocked.Increment(ref WriteCalls);
                return Task.FromResult(stream);
            }
        }

        [Test]
        public async Task Pipeline_NoProcessors_ReadReturnsBackendData()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();
            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                [],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("Test data");
            await backend.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            Stream stream = await pipeline.ReadAsync("test-uid");

            // Assert
            MemoryStream result = new MemoryStream();
            await stream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Pipeline_NoProcessors_WriteStoresInBackend()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();
            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                [],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("Test data");

            // Act
            long storedSizeBytes = await pipeline.WriteAsync(
                "test-uid",
                new MemoryStream(originalData));

            // Assert
            Stream stream = await backend.ReadAsync("test-uid");
            MemoryStream result = new MemoryStream();
            await stream.CopyToAsync(result);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(storedSizeBytes, Is.EqualTo(originalData.Length));
                Assert.That(result.ToArray(), Is.EqualTo(originalData));
            }
        }

        [Test]
        public async Task Pipeline_ProcessorsOrdered_ReadAppliesInCorrectOrder()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();

            IStorageProcessor[] processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB),
                new MarkerProcessor(50, 0xCC)   // Highest priority (lowest number)
            };

            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                processors,
                new StorageWriteAdmissionGate(1));

            // Arrange markers so that each processor actually sees its marker at the end
            // Order: CC (50), AA (100), BB (200) on read
            byte[] backendData = new byte[] { 0x01, 0xBB, 0xAA, 0xCC };
            await backend.WriteAsync("test-uid", new MemoryStream(backendData));

            // Act
            Stream stream = await pipeline.ReadAsync("test-uid");

            // Assert
            MemoryStream result = new MemoryStream();
            await stream.CopyToAsync(result);
            // Processors remove markers in order: CC (50), AA (100), BB (200)
            Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x01 }));
        }

        [Test]
        public async Task Pipeline_ProcessorsOrdered_WriteAppliesInReverseOrder()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();

            IStorageProcessor[] processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB),
                new MarkerProcessor(50, 0xCC)
            };

            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                processors,
                new StorageWriteAdmissionGate(1));

            byte[] originalData = new byte[] { 0x01 };

            // Act
            await pipeline.WriteAsync("test-uid", new MemoryStream(originalData));

            // Assert
            Stream backendStream = await backend.ReadAsync("test-uid");
            MemoryStream result = new MemoryStream();
            await backendStream.CopyToAsync(result);
            // Processors add markers in reverse order: BB (200), AA (100), CC (50)
            Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x01, 0xBB, 0xAA, 0xCC }));
        }

        [Test]
        public async Task Pipeline_DuplicateWrite_SkipsProcessors()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();
            CountingProcessor processor = new CountingProcessor();
            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                [processor],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("already stored");
            byte[] duplicateData = Encoding.UTF8.GetBytes("duplicate upload");
            await backend.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            long storedSizeBytes = await pipeline.WriteAsync(
                "test-uid",
                new MemoryStream(duplicateData));

            // Assert
            Stream stream = await backend.ReadAsync("test-uid");
            MemoryStream result = new MemoryStream();
            await stream.CopyToAsync(result);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(processor.WriteCalls, Is.Zero);
                Assert.That(storedSizeBytes, Is.EqualTo(originalData.Length));
                Assert.That(result.ToArray(), Is.EqualTo(originalData));
            }
        }

        [Test]
        public void Pipeline_ProcessorReturnsStreamNull_ThrowsInvalidOperationException()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();

            Mock<IStorageProcessor> mockProcessor = new Mock<IStorageProcessor>();
            mockProcessor.Setup(p => p.Priority).Returns(100);
            mockProcessor.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(Stream.Null);

            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                [mockProcessor.Object],
                new StorageWriteAdmissionGate(1));

            byte[] data = Encoding.UTF8.GetBytes("Test");
            backend.WriteAsync("test-uid", new MemoryStream(data)).Wait();

            // Act & Assert
            InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.ReadAsync("test-uid"));
            Assert.That(ex.Message, Does.Contain("Stream.Null"));
        }

        [Test]
        public void Pipeline_ProcessorReturnsStreamNullOnWrite_ThrowsInvalidOperationException()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();

            Mock<IStorageProcessor> mockProcessor = new Mock<IStorageProcessor>();
            mockProcessor.Setup(p => p.Priority).Returns(100);
            mockProcessor.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(Stream.Null);

            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                [mockProcessor.Object],
                new StorageWriteAdmissionGate(1));

            byte[] data = Encoding.UTF8.GetBytes("Test");

            // Act & Assert
            InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.WriteAsync("test-uid", new MemoryStream(data)));
            Assert.That(ex.Message, Does.Contain("No registered processor produced a valid stream"));
        }

        [Test]
        public async Task Pipeline_RoundTrip_WithProcessors_ReturnsOriginalData()
        {
            // Arrange
            FakeStorageBackend backend = new FakeStorageBackend();
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> logger = new Mock<ILogger<FileStoragePipeline>>();

            IStorageProcessor[] processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB)
            };

            FileStoragePipeline pipeline = new FileStoragePipeline(
                logger.Object,
                provider,
                processors,
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            await pipeline.WriteAsync("test-uid", new MemoryStream(originalData));
            Stream readStream = await pipeline.ReadAsync("test-uid");

            // Assert
            MemoryStream result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }
    }
}
