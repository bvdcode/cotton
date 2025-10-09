using System.Buffers;
using Cotton.Crypto.Models;
using System.Threading.Channels;

namespace Cotton.Crypto.Streams
{
    internal class ChannelWriteStream(ChannelWriter<ByteChunk> writer, ArrayPool<byte> pool) : Stream
    {
        private bool _completed;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public void Complete(Exception? error)
        {
            if (!_completed)
            {
                _completed = true;
                writer.TryComplete(error);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return;
            byte[] rented = pool.Rent(buffer.Length);
            buffer.Span.CopyTo(rented.AsSpan(0, buffer.Length));
            await writer.WriteAsync(new ByteChunk(rented, buffer.Length), cancellationToken).ConfigureAwait(false);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Complete(null);
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Complete(null);
            return base.DisposeAsync();
        }
    }
}