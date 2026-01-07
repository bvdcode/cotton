// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using System.Text;

namespace Cotton.Storage.Tests.Streams
{
    [TestFixture]
    public class ConcatenatedReadStreamTests
    {
        private class FakeStoragePipeline : IStoragePipeline
        {
            private readonly Dictionary<string, byte[]> _data = [];

            public void AddData(string uid, byte[] data)
            {
                _data[uid] = data;
            }

            public Task<bool> ExistsAsync(string uid)
            {
                return Task.FromResult(_data.ContainsKey(uid));
            }

            public Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
            {
                if (!_data.TryGetValue(uid, out var data))
                {
                    throw new FileNotFoundException($"UID not found: {uid}");
                }
                return Task.FromResult<Stream>(new MemoryStream(data));
            }

            public Task WriteAsync(string uid, Stream stream, PipelineContext? context = null)
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public async Task ConcatenatedReadStream_MultipleStreams_ConcatenatesCorrectly()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello "));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));
            storage.AddData("uid3", Encoding.UTF8.GetBytes("!"));

            var stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            using var reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("Hello World!"));
        }

        [Test]
        public async Task ConcatenatedReadStream_EmptyStream_SkipsCorrectly()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", []);
            storage.AddData("uid3", Encoding.UTF8.GetBytes("World"));

            var stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            using var reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("HelloWorld"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SmallBufferReads_ProducesCorrectResult()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("ABCD"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("EFGH"));

            var stream = storage.GetBlobStream(["uid1", "uid2"]);

            // Act
            var result = new List<byte>();
            byte[] buffer = new byte[3]; // Small buffer to test boundary crossing
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                result.AddRange(buffer.Take(bytesRead));
            }

            // Assert
            Assert.That(Encoding.UTF8.GetString([.. result]), Is.EqualTo("ABCDEFGH"));
        }

        [Test]
        public async Task ConcatenatedReadStream_LargeBufferReads_ProducesCorrectResult()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            var data1 = new byte[1024];
            var data2 = new byte[1024];
            for (int i = 0; i < 1024; i++)
            {
                data1[i] = (byte)(i % 256);
                data2[i] = (byte)((i + 128) % 256);
            }
            storage.AddData("uid1", data1);
            storage.AddData("uid2", data2);

            var stream = storage.GetBlobStream(["uid1", "uid2"]);

            // Act
            var result = new MemoryStream();
            await stream.CopyToAsync(result, 65536);

            // Assert
            Assert.That(result.Length, Is.EqualTo(2048));
            var resultBytes = result.ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(resultBytes.Take(1024).ToArray(), Is.EqualTo(data1));
                Assert.That(resultBytes.Skip(1024).ToArray(), Is.EqualTo(data2));
            });
        }

        [Test]
        public void ConcatenatedReadStream_StorageThrowsException_PropagatesException()
        {
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            var stream = storage.GetBlobStream(["uid1", "nonexistent"]);

            var buffer = new byte[4];
            // Read exactly available bytes from first stream
            var read1 = stream.Read(buffer, 0, 4);
            Assert.That(read1, Is.EqualTo(4));

            // Next read should trigger opening of second stream and throw
            Assert.Throws<FileNotFoundException>(() => stream.ReadExactly(buffer, 0, 1));
        }

        [Test]
        public async Task ConcatenatedReadStream_Dispose_DisablesFurtherReads()
        {
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("T"));

            var stream = storage.GetBlobStream(["uid1"]);

            // consume first byte
            var tmp = new byte[1];
            var r = await stream.ReadAsync(tmp);
            Assert.That(r, Is.EqualTo(1));

            await stream.DisposeAsync();

            var buffer = new byte[1];
            Assert.Throws<ObjectDisposedException>(() => stream.ReadExactly(buffer, 0, 1));
        }

        [Test]
        public async Task ConcatenatedReadStream_DoubleDispose_DoesNotThrow()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            var stream = storage.GetBlobStream(["uid1"]);

            // Act & Assert
            await stream.DisposeAsync();
            Assert.DoesNotThrowAsync(async () => await stream.DisposeAsync());
        }

        [Test]
        public async Task ConcatenatedReadStream_ReadAcrossBoundaries_NoGapsOrDuplicates()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", [1, 2, 3]);
            storage.AddData("uid2", [4, 5, 6]);
            storage.AddData("uid3", [7, 8, 9]);

            var stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            var result = new List<byte>();
            byte[] buffer = new byte[2]; // Read 2 bytes at a time to cross boundaries
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                result.AddRange(buffer.Take(bytesRead));
            }

            // Assert
            Assert.That(result.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        }
    }
}
