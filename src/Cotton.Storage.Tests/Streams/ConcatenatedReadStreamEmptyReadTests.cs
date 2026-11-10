// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using System.Text;

namespace Cotton.Storage.Tests.Streams
{
    public class ConcatenatedReadStreamEmptyReadTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task EmptyRead_PreservesContentAndPosition(bool seekable, bool asynchronous)
        {
            ConcatenatedReadStreamTestStorage storage = new();
            storage.AddData("first", Encoding.UTF8.GetBytes("Hello"));
            storage.AddData("second", Encoding.UTF8.GetBytes("World"));
            PipelineContext? context = seekable
                ? new PipelineContext { ChunkLengths = new() { ["first"] = 5, ["second"] = 5 } }
                : null;
            await using Stream stream = storage.GetBlobStream(["first", "second"], context);
            byte[] firstByte = new byte[1];
            await stream.ReadExactlyAsync(firstByte);

            int read = asynchronous
                ? await stream.ReadAsync(Memory<byte>.Empty)
                : stream.Read([], 0, 0);

            Assert.That(read, Is.Zero);
            Assert.That(stream.Position, Is.EqualTo(1));
            using StreamReader reader = new(stream);
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("elloWorld"));
        }
    }
}
