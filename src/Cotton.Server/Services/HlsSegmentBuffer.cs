// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class HlsSegmentBuffer : MemoryStream
    {
        private const int MaximumBytes = 64 * 1024 * 1024;

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckLength(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CheckLength(buffer.Length);
            base.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void CheckLength(int count)
        {
            if (count > MaximumBytes - Position)
            {
                throw new InvalidDataException("The encoded HLS segment exceeds the output limit.");
            }
        }
    }
}
