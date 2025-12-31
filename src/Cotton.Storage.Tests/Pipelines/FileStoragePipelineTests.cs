// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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

            public Task<bool> DeleteAsync(string uid)
            {
                return Task.FromResult(_storage.Remove(uid));
            }

            public Task<Stream> ReadAsync(string uid)
            {
                if (!_storage.TryGetValue(uid, out var data))
                {
                    throw new FileNotFoundException($"UID not found: {uid}");
                }
                return Task.FromResult<Stream>(new MemoryStream(data));
            }

            public Task WriteAsync(string uid, Stream stream)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                _storage[uid] = ms.ToArray();
                return Task.CompletedTask;
            }
        }

        private class FakeBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            public IStorageBackend GetBackend() => backend;
        }

        private class MarkerProcessor(int priority, byte marker) : IStorageProcessor
        {
            public int Priority => priority;

            public async Task<Stream> ReadAsync(string uid, Stream stream)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var data = ms.ToArray();

                // Remove marker from end
                if (data.Length > 0 && data[^1] == marker)
                {
                    return new MemoryStream(data[..^1]) { Position = 0 };
                }

                return new MemoryStream(data) { Position = 0 };
            }

            public async Task<Stream> WriteAsync(string uid, Stream stream)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                _ = ms.ToArray();

                // Add marker to end
                ms.WriteByte(marker);
                ms.Position = 0;
                return ms;
            }
        }

        [Test]
        public async Task Pipeline_NoProcessors_ReadReturnsBackendData()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();
            var pipeline = new FileStoragePipeline(logger.Object, provider, []);

            var originalData = Encoding.UTF8.GetBytes("Test data");
            await backend.WriteAsync("test-uid", new MemoryStream(originalData));

            // Act
            var stream = await pipeline.ReadAsync("test-uid");

            // Assert
            var result = new MemoryStream();
            await stream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Pipeline_NoProcessors_WriteStoresInBackend()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();
            var pipeline = new FileStoragePipeline(logger.Object, provider, []);

            var originalData = Encoding.UTF8.GetBytes("Test data");

            // Act
            await pipeline.WriteAsync("test-uid", new MemoryStream(originalData));

            // Assert
            var stream = await backend.ReadAsync("test-uid");
            var result = new MemoryStream();
            await stream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Pipeline_ProcessorsOrdered_ReadAppliesInCorrectOrder()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();

            var processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB),
                new MarkerProcessor(50, 0xCC)   // Highest priority (lowest number)
            };

            var pipeline = new FileStoragePipeline(logger.Object, provider, processors);

            // Arrange markers so that each processor actually sees its marker at the end
            // Order: CC (50), AA (100), BB (200) on read
            var backendData = new byte[] { 0x01, 0xBB, 0xAA, 0xCC };
            await backend.WriteAsync("test-uid", new MemoryStream(backendData));

            // Act
            var stream = await pipeline.ReadAsync("test-uid");

            // Assert
            var result = new MemoryStream();
            await stream.CopyToAsync(result);
            // Processors remove markers in order: CC (50), AA (100), BB (200)
            Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x01 }));
        }

        [Test]
        public async Task Pipeline_ProcessorsOrdered_WriteAppliesInReverseOrder()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();

            var processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB),
                new MarkerProcessor(50, 0xCC)
            };

            var pipeline = new FileStoragePipeline(logger.Object, provider, processors);

            var originalData = new byte[] { 0x01 };

            // Act
            await pipeline.WriteAsync("test-uid", new MemoryStream(originalData));

            // Assert
            var backendStream = await backend.ReadAsync("test-uid");
            var result = new MemoryStream();
            await backendStream.CopyToAsync(result);
            // Processors add markers in reverse order: BB (200), AA (100), CC (50)
            Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 0x01, 0xBB, 0xAA, 0xCC }));
        }

        [Test]
        public void Pipeline_ProcessorReturnsStreamNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();

            var mockProcessor = new Mock<IStorageProcessor>();
            mockProcessor.Setup(p => p.Priority).Returns(100);
            mockProcessor.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(Stream.Null);

            var pipeline = new FileStoragePipeline(logger.Object, provider, [mockProcessor.Object]);

            var data = Encoding.UTF8.GetBytes("Test");
            backend.WriteAsync("test-uid", new MemoryStream(data)).Wait();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.ReadAsync("test-uid"));
            Assert.That(ex.Message, Does.Contain("Stream.Null"));
        }

        [Test]
        public void Pipeline_ProcessorReturnsStreamNullOnWrite_ThrowsInvalidOperationException()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();

            var mockProcessor = new Mock<IStorageProcessor>();
            mockProcessor.Setup(p => p.Priority).Returns(100);
            mockProcessor.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(Stream.Null);

            var pipeline = new FileStoragePipeline(logger.Object, provider, [mockProcessor.Object]);

            var data = Encoding.UTF8.GetBytes("Test");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.WriteAsync("test-uid", new MemoryStream(data)));
            Assert.That(ex.Message, Does.Contain("No registered processor produced a valid stream"));
        }

        [Test]
        public async Task Pipeline_RoundTrip_WithProcessors_ReturnsOriginalData()
        {
            // Arrange
            var backend = new FakeStorageBackend();
            var provider = new FakeBackendProvider(backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();

            var processors = new IStorageProcessor[]
            {
                new MarkerProcessor(100, 0xAA),
                new MarkerProcessor(200, 0xBB)
            };

            var pipeline = new FileStoragePipeline(logger.Object, provider, processors);

            var originalData = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            await pipeline.WriteAsync("test-uid", new MemoryStream(originalData));
            var readStream = await pipeline.ReadAsync("test-uid");

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }
    }
}
