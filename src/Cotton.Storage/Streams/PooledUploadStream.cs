// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;

namespace Cotton.Storage.Streams
{
    internal class PooledUploadStream : Stream
    {
        private const int BufferSize = 8 * 1024 * 1024;
        private const int MaxBuffers = 16;
        private static readonly ArrayPool<byte> Buffers = ArrayPool<byte>.Create(BufferSize, MaxBuffers);
        private static readonly SemaphoreSlim Slots = new(MaxBuffers, MaxBuffers);
        private static readonly SemaphoreSlim Filling = new(1, 1);
        private readonly List<byte[]> _buffers = new(MaxBuffers);
        private long _length;
        private long _position;
        private bool _disposed;

        public static async Task<PooledUploadStream> CreateAsync(Stream source, CancellationToken cancellationToken)
        {
            PooledUploadStream stream = new();
            await Filling.WaitAsync(cancellationToken).ConfigureAwait(false);
            bool completed = false;
            try
            {
                if (source.CanSeek)
                {
                    source.Seek(0, SeekOrigin.Begin);
                }

                byte[] probe = new byte[1];
                while (await source.ReadAsync(probe, cancellationToken).ConfigureAwait(false) > 0)
                {
                    if (stream._buffers.Count == MaxBuffers)
                    {
                        throw new IOException($"S3 object exceeds the {MaxBuffers * BufferSize} byte upload buffer budget.");
                    }

                    await Slots.WaitAsync(cancellationToken).ConfigureAwait(false);
                    bool rented = false;
                    try
                    {
                        byte[] buffer = Buffers.Rent(BufferSize);
                        stream._buffers.Add(buffer);
                        rented = true;
                        buffer[0] = probe[0];
                        int read = await source.ReadAtLeastAsync(buffer.AsMemory(1, BufferSize - 1), BufferSize - 1,
                            throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                        stream._length += read + 1;
                    }
                    finally
                    {
                        if (!rented)
                        {
                            Slots.Release();
                        }
                    }
                }

                completed = true;
                return stream;
            }
            finally
            {
                if (!completed)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                Filling.Release();
            }
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;
        public override long Length
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _length;
            }
        }

        public override long Position
        {
            get => _position;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _position = value;
            }
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_position >= _length)
            {
                return 0;
            }

            int count = (int)Math.Min(buffer.Length, _length - _position);
            int copied = 0;
            while (copied < count)
            {
                int index = (int)(_position / BufferSize);
                int offset = (int)(_position % BufferSize);
                int length = Math.Min(count - copied, BufferSize - offset);
                _buffers[index].AsSpan(offset, length).CopyTo(buffer[copied..]);
                _position += length;
                copied += length;
            }
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return Read(buffer.AsSpan(offset, count));
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (position < 0)
            {
                throw new IOException("Cannot seek before the start of the stream.");
            }
            _position = position;
            return position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                foreach (byte[] buffer in _buffers)
                {
                    Buffers.Return(buffer, clearArray: true);
                    Slots.Release();
                }
                _buffers.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
