// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Processors;

namespace Cotton.Storage.Tests.Processors
{
    public class ZstandardFormatTests
    {
        private const string EmptyFrame = "KLUv/QBIAQAA";
        private const string OneByteFrame = "KLUv/QBICAAAAAEAAA==";
        private const string RepeatedSequenceFrame =
            "KLUv/QBILAgAtA8AAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkJSYnKCkqKywtLi8w" +
            "MTIzNDU2Nzg5Ojs8PT4/QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamts" +
            "bW5vcHFyc3R1dnd4eXp7fH1+f4CBgoOEhYaHiImKi4yNjo+QkZKTlJWWl5iZmpucnZ6foKGio6Sl" +
            "pqeoqaqrrK2ur7CxsrO0tba3uLm6u7y9vr/AwcLDxMXGx8jJysvMzc7P0NHS09TV1tfY2drb3N3e" +
            "3+Dh4uPk5ebn6Onq6+zt7u/w8fLz9PX29/j5+gEAe4H7fSMFAQAA";

        [TestCase(EmptyFrame, 0)]
        [TestCase(OneByteFrame, 1)]
        [TestCase(RepeatedSequenceFrame, 4096)]
        public async Task Read_ExistingZstdSharpFrame_RestoresPayload(string frame, int length)
        {
            CompressionProcessor processor = new();
            using MemoryStream input = new(Convert.FromBase64String(frame), writable: false);
            await using Stream decompressed = await processor.ReadAsync("saved-frame", input);
            using MemoryStream output = new();

            await decompressed.CopyToAsync(output);

            byte[] expected = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
            Assert.That(output.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public async Task Read_ConcatenatedFrames_RestoresEveryFrame()
        {
            byte[] frame = Convert.FromBase64String(OneByteFrame);
            using MemoryStream input = new([.. frame, .. frame], writable: false);
            CompressionProcessor processor = new();
            await using Stream decompressed = await processor.ReadAsync("concatenated-frames", input);
            using MemoryStream output = new();

            await decompressed.CopyToAsync(output);

            Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 0, 0 }));
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(16)]
        public async Task Read_TruncatedFrame_RejectsIncompletePayload(int missingBytes)
        {
            byte[] frame = Convert.FromBase64String(RepeatedSequenceFrame);
            using MemoryStream input = new(frame, 0, frame.Length - missingBytes, writable: false);
            CompressionProcessor processor = new();
            await using Stream decompressed = await processor.ReadAsync("truncated-frame", input);

            await Assert.ThrowsAsync<InvalidDataException>(async () => await decompressed.CopyToAsync(Stream.Null));
        }

        [Test]
        public async Task Read_Cancelled_PropagatesCancellation()
        {
            using MemoryStream input = new(Convert.FromBase64String(RepeatedSequenceFrame), writable: false);
            CompressionProcessor processor = new();
            await using Stream decompressed = await processor.ReadAsync("cancelled-read", input);
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            await Assert.CatchAsync<OperationCanceledException>(async () =>
                await decompressed.CopyToAsync(Stream.Null, cancellation.Token));
        }
    }
}
