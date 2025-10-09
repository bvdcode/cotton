using System.Buffers;
using Cotton.Crypto.Models;
using System.Threading.Channels;

namespace Cotton.Crypto.Streams
{
    internal class ChannelReadStream(ChannelReader<ByteChunk> reader, Stream input, byte[] fileKey, Task bgTask, CancellationTokenSource cts, ArrayPool<byte> bufferPool, bool leaveInputOpen) : Stream
    {
        private byte[]? _current;
        private int _currentLength;
        private int _pos;
        private bool _disposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ChannelReadStream));

            if (_current is null || _pos >= _currentLength)
            {
                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                if (!reader.TryRead(out var chunk))
                {
                    chunk = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }

                _current = chunk.Buffer;
                _currentLength = chunk.Length;
                _pos = 0;
            }

            int toCopy = Math.Min(buffer.Length, _currentLength - _pos);
            _current.AsSpan(_pos, toCopy).CopyTo(buffer.Span);
            _pos += toCopy;

            if (_pos >= _currentLength && _current != null)
            {
                bufferPool.Return(_current);
                _current = null;
                _currentLength = 0;
                _pos = 0;
            }

            return toCopy;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count))
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    cts.Cancel();
                }
                catch
                {
                }

                if (_current != null)
                {
                    bufferPool.Return(_current);
                    _current = null;
                    _currentLength = 0;
                    _pos = 0;
                }

                try
                {
                    await bgTask.ConfigureAwait(false);
                }
                catch
                {
                }

                Array.Clear(fileKey, 0, fileKey.Length);
                bufferPool.Return(fileKey);

                if (!leaveInputOpen)
                {
                    try
                    {
                        await input.DisposeAsync()
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

                cts.Dispose();
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
