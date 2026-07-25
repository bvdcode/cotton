// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;
using Cotton.Previews.Tests.TestInfrastructure;

namespace Cotton.Previews.Tests
{
    public class BoundedProcessOutputReaderTests
    {
        [Test]
        public async Task ReadAsync_OutputWithinLimit_ReturnsCompleteText()
        {
            byte[] content = Encoding.UTF8.GetBytes("bounded output");
            using MemoryStream stream = new(content);
            BoundedProcessOutputReader reader = new("stdout", content.Length);

            string result = await reader.ReadAsync(stream, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("bounded output"));
                Assert.That(reader.LimitExceeded.IsCompleted, Is.False);
            });
        }

        [Test]
        public async Task ReadAsync_OutputExceedsLimit_SignalsLimitAndBoundsCapture()
        {
            byte[] content = Encoding.UTF8.GetBytes("0123456789");
            using MemoryStream stream = new(content);
            BoundedProcessOutputReader reader = new("stderr", maxBytes: 4);

            string result = await reader.ReadAsync(stream, CancellationToken.None);
            FfprobeOutputLimitExceededException exception = await reader.LimitExceeded;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("0123"));
                Assert.That(exception.OutputName, Is.EqualTo("stderr"));
                Assert.That(exception.MaxBytes, Is.EqualTo(4));
            });
        }

        [Test]
        [NonParallelizable]
        public async Task ReadAsync_LargeSource_DoesNotAllocateProportionallyToInput()
        {
            const long sourceLength = 64L * 1024 * 1024;
            const int captureLimit = 1024;
            RepeatingReadStream stream = new(sourceLength, (byte)'x');
            BoundedProcessOutputReader reader = new("stdout", captureLimit);

            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            string result = await reader.ReadAsync(stream, CancellationToken.None);
            long allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

            Assert.Multiple(() =>
            {
                Assert.That(stream.BytesRead, Is.EqualTo(sourceLength));
                Assert.That(Encoding.UTF8.GetByteCount(result), Is.EqualTo(captureLimit));
                Assert.That(reader.LimitExceeded.IsCompletedSuccessfully, Is.True);
                Assert.That(allocatedBytes, Is.LessThan(4L * 1024 * 1024));
            });
        }
    }
}
