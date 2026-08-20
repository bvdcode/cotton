// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using System.Text;

namespace Cotton.Storage.Tests.Streams
{
    [TestFixture]
    public class ConcatenatedReadStreamRangeTests
    {
        [Test]
        public async Task ConcatenatedReadStream_ReadAcrossChunkBoundariesWithSeek_NoGaps()
        {
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("uid1", Encoding.UTF8.GetBytes("ABC"));
            storage.AddData("uid2", Encoding.UTF8.GetBytes("DEF"));

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = 6,
                ChunkLengths = new Dictionary<string, long>
                {
                    ["uid1"] = 3,
                    ["uid2"] = 3
                }
            };

            Stream stream = storage.GetBlobStream(["uid1", "uid2"], context);
            stream.Seek(2, SeekOrigin.Begin);
            byte[] buffer = new byte[3];
            int read = await stream.ReadAsync(buffer);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(3));
                Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("CDE"));
            });
        }

        [Test]
        public async Task ConcatenatedReadStream_RandomRanges_MatchReferenceFile()
        {
            const int chunkSize = 8 * 1024 * 1024;
            const int rangeOperations = 10_000;
            Random random = new(12345);
            int fileLength = (chunkSize * 2) + 123_456;
            byte[] fileBytes = new byte[fileLength];
            random.NextBytes(fileBytes);

            ConcatenatedReadStreamTestStorage storage = new();
            List<string> uids = [];
            Dictionary<string, long> chunkLengths = new(StringComparer.OrdinalIgnoreCase);
            for (int offset = 0, index = 0; offset < fileBytes.Length; offset += chunkSize, index++)
            {
                int length = Math.Min(chunkSize, fileBytes.Length - offset);
                byte[] chunk = new byte[length];
                Buffer.BlockCopy(fileBytes, offset, chunk, 0, length);

                string uid = $"uid{index}";
                uids.Add(uid);
                chunkLengths[uid] = length;
                storage.AddData(uid, chunk);
            }

            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = fileBytes.Length,
                ChunkLengths = chunkLengths,
            };

            await using Stream stream = storage.GetBlobStream([.. uids], context);
            for (int index = 0; index < rangeOperations; index++)
            {
                int start = random.Next(0, fileBytes.Length);
                int remaining = fileBytes.Length - start;
                int length = random.Next(0, remaining + 1);

                stream.Seek(start, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                await stream.ReadExactlyAsync(buffer);

                if (!buffer.AsSpan().SequenceEqual(fileBytes.AsSpan(start, length)))
                {
                    Assert.Fail($"Mismatch at op={index}, start={start}, len={length}.");
                }
            }
        }
    }
}
