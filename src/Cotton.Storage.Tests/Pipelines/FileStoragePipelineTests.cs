// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using System.Text;

namespace Cotton.Storage.Tests.Pipelines
{
    [TestFixture]
    public class FileStoragePipelineTests
    {
        private class FakeStorageBackend : IStorageBackend
        {
            private readonly Dictionary<string, byte[]> _storage = new();

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

        private class MarkerProcessor : IStorageProcessor
        {
            private readonly int _priority;
            private readonly byte _marker;

            public MarkerProcessor(int priority, byte marker)
            {
                _priority = priority;
                _marker = marker;
            }

            public int Priority => _priority;

            public async Task<Stream> ReadAsync(string uid, Stream stream)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var data = ms.ToArray();
                
                // Remove marker from end
                if (data.Length > 0 && data[^1] == _marker)
                {
                    return new MemoryStream(data[..^1]) { Position = 0 };
                }
                
                return new MemoryStream(data) { Position = 0 };
            }

            public async Task<Stream> WriteAsync(string uid, Stream stream)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var data = ms.ToArray();
                
                // Add marker to end
                ms.WriteByte(_marker);
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
            var pipeline = new FileStoragePipeline(logger.Object, provider, Array.Empty<IStorageProcessor>());

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
            var pipeline = new FileStoragePipeline(logger.Object, provider, Array.Empty<IStorageProcessor>());

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
                new MarkerProcessor(100, 0xAA), // Lower priority = applied first on read
                new MarkerProcessor(200, 0xBB),
                new MarkerProcessor(50, 0xCC)   // Highest priority (lowest number)
            };
            
            var pipeline = new FileStoragePipeline(logger.Object, provider, processors);

            // Backend has data with markers: CC, AA, BB (reverse order of write)
            var backendData = new byte[] { 0x01, 0xCC, 0xAA, 0xBB };
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
            
            var pipeline = new FileStoragePipeline(logger.Object, provider, new[] { mockProcessor.Object });

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
            
            var pipeline = new FileStoragePipeline(logger.Object, provider, new[] { mockProcessor.Object });

            var data = Encoding.UTF8.GetBytes("Test");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.WriteAsync("test-uid", new MemoryStream(data)));
            Assert.That(ex.Message, Does.Contain("Stream.Null"));
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
