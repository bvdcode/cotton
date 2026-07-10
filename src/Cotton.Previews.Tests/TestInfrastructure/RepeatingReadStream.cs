// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests.TestInfrastructure
{
    internal class RepeatingReadStream(long length, byte value) : Stream
    {
        private long _position;

        public long BytesRead => _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = GetBytesToRead(count);
            buffer.AsSpan(offset, bytesToRead).Fill(value);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesToRead = GetBytesToRead(buffer.Length);
            buffer.Span[..bytesToRead].Fill(value);
            _position += bytesToRead;
            return ValueTask.FromResult(bytesToRead);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private int GetBytesToRead(int requested)
        {
            long remaining = length - _position;
            return remaining <= 0
                ? 0
                : checked((int)Math.Min(requested, remaining));
        }
    }
}
