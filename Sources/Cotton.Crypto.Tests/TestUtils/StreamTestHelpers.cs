using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cotton.Crypto.Tests.TestUtils
{
    internal class NonSeekableReadStream(Stream inner) : Stream
    {
        private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // Do not dispose inner to mimic typical stream wrappers unless needed
        }
    }

    internal class SlowWriteStream(Stream inner, int delayMs) : Stream
    {
        public Stream Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

        public override bool CanRead => false;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => true;
        public override long Length => Inner.Length;
        public override long Position { get => Inner.Position; set => Inner.Position = value; }
        public override void Flush() => Inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => Inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(delayMs);
            Inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            await Inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            await Inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}
