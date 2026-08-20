// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using System.Text;

namespace Cotton.Storage.Tests.Streams
{
    [TestFixture]
    public class ConcatenatedReadStreamTests
    {
        [Test]
        public async Task ConcatenatedReadStream_MultipleStreams_ConcatenatesCorrectly()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello "));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));
            storage.AddData("uid3", Encoding.UTF8.GetBytes("!"));

            Stream stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("Hello World!"));
        }

        [Test]
        public async Task ConcatenatedReadStream_EmptyStream_SkipsCorrectly()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", []);
            storage.AddData("uid3", Encoding.UTF8.GetBytes("World"));

            Stream stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("HelloWorld"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SmallBufferReads_ProducesCorrectResult()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("ABCD"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("EFGH"));

            Stream stream = storage.GetBlobStream(["uid1", "uid2"]);

            // Act
            List<byte> result = new List<byte>();
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
            ConcatenatedReadStreamTestStorage storage = new();
            byte[] data1 = new byte[1024];
            byte[] data2 = new byte[1024];
            for (int i = 0; i < 1024; i++)
            {
                data1[i] = (byte)(i % 256);
                data2[i] = (byte)((i + 128) % 256);
            }
            storage.AddData("uid1", data1);
            storage.AddData("uid2", data2);

            Stream stream = storage.GetBlobStream(["uid1", "uid2"]);

            // Act
            MemoryStream result = new MemoryStream();
            await stream.CopyToAsync(result, 65536);

            // Assert
            Assert.That(result.Length, Is.EqualTo(2048));
            byte[] resultBytes = result.ToArray();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resultBytes.Take(1024).ToArray(), Is.EqualTo(data1));
                Assert.That(resultBytes.Skip(1024).ToArray(), Is.EqualTo(data2));
            }
        }

        [Test]
        public void ConcatenatedReadStream_StorageThrowsException_PropagatesException()
        {
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            Stream stream = storage.GetBlobStream(["uid1", "nonexistent"]);

            byte[] buffer = new byte[4];
            // Read exactly available bytes from first stream
            int read1 = stream.Read(buffer, 0, 4);
            Assert.That(read1, Is.EqualTo(4));

            // Next read should trigger opening of second stream and throw
            Assert.Throws<FileNotFoundException>(() => stream.ReadExactly(buffer, 0, 1));
        }

        [Test]
        public async Task ConcatenatedReadStream_Dispose_DisablesFurtherReads()
        {
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("T"));

            Stream stream = storage.GetBlobStream(["uid1"]);

            // consume first byte
            byte[] tmp = new byte[1];
            int r = await stream.ReadAsync(tmp);
            Assert.That(r, Is.EqualTo(1));

            await stream.DisposeAsync();

            byte[] buffer = new byte[1];
            Assert.Throws<ObjectDisposedException>(() => stream.ReadExactly(buffer, 0, 1));
        }

        [Test]
        public async Task ConcatenatedReadStream_DoubleDispose_DoesNotThrow()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            Stream stream = storage.GetBlobStream(["uid1"]);

            // Act & Assert
            await stream.DisposeAsync();
            Assert.DoesNotThrowAsync(async () => await stream.DisposeAsync());
        }

        [Test]
        public async Task ConcatenatedReadStream_ReadAcrossBoundaries_NoGapsOrDuplicates()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", [1, 2, 3]);
            storage.AddData("uid2", [4, 5, 6]);
            storage.AddData("uid3", [7, 8, 9]);

            Stream stream = storage.GetBlobStream(["uid1", "uid2", "uid3"]);

            // Act
            List<byte> result = new List<byte>();
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
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Test"));

            Stream stream = storage.GetBlobStream(["uid1"]);

            // Assert
            Assert.That(stream.CanSeek, Is.False);
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public void ConcatenatedReadStream_WithChunkLengths_CanSeekIsTrue()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

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
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(5, SeekOrigin.Begin); // Jump to second chunk
            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekCurrent_ReadsFromCorrectPosition()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            byte[] buffer = new byte[2];
            await stream.ReadExactlyAsync(buffer); // Read "He"
            stream.Seek(3, SeekOrigin.Current); // Skip "llo"

            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekEnd_ReadsFromCorrectPosition()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(-5, SeekOrigin.End); // Jump to "World"
            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            // Assert
            Assert.That(result, Is.EqualTo("World"));
        }

        [Test]
        public async Task ConcatenatedReadStream_SeekWithinChunk_ReadsCorrectly()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(7, SeekOrigin.Begin); // "o" in "World"
            byte[] buffer = new byte[3];
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
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act
            stream.Seek(7, SeekOrigin.Begin);
            byte[] buffer1 = new byte[3];
            await stream.ReadExactlyAsync(buffer1); // Read "rld"

            stream.Seek(2, SeekOrigin.Begin); // Go back to "llo"
            byte[] buffer2 = new byte[3];
            await stream.ReadExactlyAsync(buffer2);

            // Assert
            Assert.That(Encoding.UTF8.GetString(buffer2), Is.EqualTo("llo"));
        }

        [Test]
        public void ConcatenatedReadStream_SeekBeforeStart_ThrowsException()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 5,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1"], context);

            // Assert
            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }

        [Test]
        public void ConcatenatedReadStream_SeekAfterEnd_ThrowsException()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 5,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1"], context);

            // Assert
            Assert.Throws<IOException>(() => stream.Seek(6, SeekOrigin.Begin));
        }

        [Test]
        public async Task ConcatenatedReadStream_PositionProperty_WorksCorrectly()
        {
            // Arrange
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("World"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 10,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 5,
                    ["uid2"] = 5
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);

            // Act & Assert
            Assert.That(stream.Position, Is.Zero);

            byte[] buffer = new byte[3];
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
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", [1, 2, 3]);
            storage.AddData("uid2", [4, 5, 6]);
            storage.AddData("uid3", [7, 8, 9]);

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 9,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 3,
                    ["uid2"] = 3,
                    ["uid3"] = 3
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2", "uid3"], context);

            // Act - Jump around and read
            stream.Seek(4, SeekOrigin.Begin); // Middle of second chunk
            byte[] buffer1 = new byte[1];
            await stream.ReadExactlyAsync(buffer1);
            Assert.That(buffer1[0], Is.EqualTo(5));

            stream.Seek(0, SeekOrigin.Begin); // Back to start
            byte[] buffer2 = new byte[1];
            await stream.ReadExactlyAsync(buffer2);
            Assert.That(buffer2[0], Is.EqualTo(1));

            stream.Seek(8, SeekOrigin.Begin); // Last byte
            byte[] buffer3 = new byte[1];
            await stream.ReadExactlyAsync(buffer3);
            Assert.That(buffer3[0], Is.EqualTo(9));
        }

    }
}
