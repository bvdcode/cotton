// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    internal class PrefixedReadStream(ReadOnlyMemory<byte> prefix, Stream source, long? maxBytes = null) : Stream
    {
        private readonly byte[] _probe = new byte[1];
        private int _prefixOffset;
        private long _bytesRead;
        private bool _truncationChecked;

        public bool IsTruncated { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }
            int allowed = GetAllowedCount(buffer.Length);
            if (allowed == 0)
            {
                DetectTruncation();
                return 0;
            }
            int read = ReadPrefix(buffer[..allowed]);
            if (read == 0)
            {
                read = source.Read(buffer[..allowed]);
            }
            _bytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }
            int allowed = GetAllowedCount(buffer.Length);
            if (allowed == 0)
            {
                await DetectTruncationAsync(cancellationToken);
                return 0;
            }
            int read = ReadPrefix(buffer.Span[..allowed]);
            if (read == 0)
            {
                read = await source.ReadAsync(buffer[..allowed], cancellationToken);
            }
            _bytesRead += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int GetAllowedCount(int requested)
        {
            if (maxBytes is not long limit)
            {
                return requested;
            }
            return (int)Math.Min(requested, Math.Max(0, limit - _bytesRead));
        }

        private int ReadPrefix(Span<byte> buffer)
        {
            int count = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
            if (count > 0)
            {
                prefix.Span.Slice(_prefixOffset, count).CopyTo(buffer);
                _prefixOffset += count;
            }
            return count;
        }

        private void DetectTruncation()
        {
            if (!_truncationChecked && (_prefixOffset < prefix.Length || source.Read(_probe) > 0))
            {
                IsTruncated = true;
            }
            _truncationChecked = true;
        }

        private async Task DetectTruncationAsync(CancellationToken cancellationToken)
        {
            if (!_truncationChecked && (_prefixOffset < prefix.Length
                || await source.ReadAsync(_probe.AsMemory(), cancellationToken) > 0))
            {
                IsTruncated = true;
            }
            _truncationChecked = true;
        }
    }
}
