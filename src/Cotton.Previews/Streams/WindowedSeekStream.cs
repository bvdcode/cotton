namespace Cotton.Previews.Streams
{
    internal sealed class WindowedSeekStream(Stream inner, long start, long length, bool leaveOpen = true) : Stream
    {
        private readonly bool _leaveOpen = leaveOpen;
        private long _pos;
        private readonly long _end = start + length;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _pos;
            set => throw new NotSupportedException();
        }

        private void EnsurePositioned()
        {
            if (inner.CanSeek)
            {
                inner.Seek(start + _pos, SeekOrigin.Begin);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= length)
            {
                return 0;
            }

            EnsurePositioned();
            int toRead = (int)Math.Min(count, length - _pos);
            int read = inner.Read(buffer, offset, toRead);
            _pos += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_pos >= length)
            {
                return 0;
            }

            EnsurePositioned();
            int toRead = (int)Math.Min(buffer.Length, length - _pos);
            int read = await inner.ReadAsync(buffer[..toRead], cancellationToken).ConfigureAwait(false);
            _pos += read;
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
