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

            public Task<bool> DeleteAsync(string uid)
            {
                bool removed = _data.Remove(uid);
                return Task.FromResult(removed);
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resultBytes.Take(1024).ToArray(), Is.EqualTo(data1));
                Assert.That(resultBytes.Skip(1024).ToArray(), Is.EqualTo(data2));
            }
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

        [Test]
        public void ConcatenatedReadStream_WithoutChunkLengths_CanSeekIsFalse()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            var stream = storage.GetBlobStream(["uid1"]);

            // Assert
            Assert.That(stream.CanSeek, Is.False);
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public void ConcatenatedReadStream_WithChunkLengths_CanSeekIsTrue()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(stream.CanSeek, Is.True);
                Assert.That(stream.Length, Is.EqualTo(10));
            }
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekBegin_ReadsFromCorrectPosition()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(5, SeekOrigin.Begin); // Jump to second chunk
            using var reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekCurrent_ReadsFromCorrectPosition()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            var buffer = new byte[2];
            await stream.ReadExactlyAsync(buffer); // Read "He"
            stream.Seek(3, SeekOrigin.Current); // Skip "llo"

            using var reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekEnd_ReadsFromCorrectPosition()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(-5, SeekOrigin.End); // Jump to "World"
            using var reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekWithinChunk_ReadsCorrectly()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(7, SeekOrigin.Begin); // "o" in "World"
            var buffer = new byte[3];
            int read = await stream.ReadAsync(buffer);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(read, Is.EqualTo(3));
                Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("rld"));
            }
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekBackward_ReadsCorrectly()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(7, SeekOrigin.Begin);
            var buffer1 = new byte[3];
            await stream.ReadExactlyAsync(buffer1); // Read "rld"

            stream.Seek(2, SeekOrigin.Begin); // Go back to "llo"
            var buffer2 = new byte[3];
            await stream.ReadExactlyAsync(buffer2);

            // Assert
            Assert.That(Encoding.UTF8.GetString(buffer2), Is.EqualTo("llo"));
        }

        [Test]
        public void ConcatenatedReadStream_SeekBeforeStart_ThrowsException()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));

            var context = new PipelineContext
            {
                FileSizeBytes = 5,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1"], context);

            // Assert
            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }

        [Test]
        public void ConcatenatedReadStream_SeekAfterEnd_ThrowsException()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));

            var context = new PipelineContext
            {
                FileSizeBytes = 5,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1"], context);

            // Assert
            Assert.Throws<IOException>(() => stream.Seek(6, SeekOrigin.Begin));
        }

        [Test]
        public async Task ConcatenatedReadStream_PositionProperty_WorksCorrectly()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            var context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act & Assert
            Assert.That(stream.Position, Is.Zero);

            var buffer = new byte[3];
            await stream.ReadExactlyAsync(buffer);
            Assert.That(stream.Position, Is.EqualTo(3));

            stream.Position = 7;
            Assert.That(stream.Position, Is.EqualTo(7));

            await stream.ReadExactlyAsync(buffer);
            Assert.That(stream.Position, Is.EqualTo(10));
        }

        [Test]
        public async Task ConcatenatedReadStream_MultipleChunks_SeekAndRead()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", [1, 2, 3]);
            storage.AddData("uid2", [4, 5, 6]);
            storage.AddData("uid3", [7, 8, 9]);

            var context = new PipelineContext
            {
                FileSizeBytes = 9,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 3,
                    ["uid2"] = 3,
                    ["uid3"] = 3
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2", "uid3"], context);

            // Act - Jump around and read
            stream.Seek(4, SeekOrigin.Begin); // Middle of second chunk
            var buffer1 = new byte[1];
            await stream.ReadExactlyAsync(buffer1);
            Assert.That(buffer1[0], Is.EqualTo(5));

            stream.Seek(0, SeekOrigin.Begin); // Back to start
            var buffer2 = new byte[1];
            await stream.ReadExactlyAsync(buffer2);
            Assert.That(buffer2[0], Is.EqualTo(1));

            stream.Seek(8, SeekOrigin.Begin); // Last byte
            var buffer3 = new byte[1];
            await stream.ReadExactlyAsync(buffer3);
            Assert.That(buffer3[0], Is.EqualTo(9));
        }

        [Test]
        public async Task ConcatenatedReadStream_ReadAcrossChunkBoundariesWithSeek_NoGaps()
        {
            // Arrange
            var storage = new FakeStoragePipeline();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("ABC"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("DEF"));

            var context = new PipelineContext
            {
                FileSizeBytes = 6,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 3,
                    ["uid2"] = 3
                }
            };

            var stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act - Seek to near boundary and read across
            stream.Seek(2, SeekOrigin.Begin);
            var buffer = new byte[3];
            int read = await stream.ReadAsync(buffer);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(read, Is.EqualTo(3));
                Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("CDE"));
            }
        }

        [Test]
        public async Task ConcatenatedReadStream_RandomRanges_MatchReferenceFile()
        {
            const int chunkSize = 8 * 1024 * 1024;
            const int rangeOps = 10_000;

            var rng = new Random(12345);
            int fileLength = (chunkSize * 2) + 123_456;

            var fileBytes = new byte[fileLength];
            rng.NextBytes(fileBytes);

            var storage = new FakeStoragePipeline();
            var uids = new List<string>();
            var chunkLengths = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (int offset = 0, index = 0; offset < fileBytes.Length; offset += chunkSize, index++)
            {
                int len = Math.Min(chunkSize, fileBytes.Length - offset);
                var chunk = new byte[len];
                Buffer.BlockCopy(fileBytes, offset, chunk, 0, len);

                string uid = $"uid{index}";
                uids.Add(uid);
                chunkLengths[uid] = len;
                storage.AddData(uid, chunk);
            }

            var context = new PipelineContext
            {
                FileSizeBytes = fileBytes.Length,
                ChunkLengths = chunkLengths,
            };

            await using var stream = storage.GetBlobStream([.. uids], context);

            for (int i = 0; i < rangeOps; i++)
            {
                int start = rng.Next(0, fileBytes.Length);
                int remaining = fileBytes.Length - start;
                int len = rng.Next(0, remaining + 1);

                stream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[len];
                await stream.ReadExactlyAsync(buffer);

                var expected = fileBytes.AsSpan(start, len);
                if (!buffer.AsSpan().SequenceEqual(expected))
                {
                    Assert.Fail($"Mismatch at op={i}, start={start}, len={len}.");
                }
            }
        }
    }
}
