// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class ArchiveRangeWriteStream(Stream destination, long start, long length) : Stream
    {
        private long _position;
        public long Start { get; } = start;
        public long End { get; } = checked(start + length);
        public bool Completed => _position >= End;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public bool Includes(long offset, long count) => offset < End && offset + count > Start;

        public void Advance(long count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            _position = checked(_position + count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int skip = (int)Math.Clamp(Start - _position, 0, buffer.Length);
            int count = (int)Math.Clamp(End - _position - skip, 0, buffer.Length - skip);
            if (count > 0)
            {
                await destination.WriteAsync(buffer.Slice(skip, count), cancellationToken);
            }
            Advance(buffer.Length);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override Task FlushAsync(CancellationToken cancellationToken) => destination.FlushAsync(cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
