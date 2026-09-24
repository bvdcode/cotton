// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    internal class JsonArrayReadStream(Stream source) : Stream
    {
        private readonly byte[] _sourceBuffer = new byte[81920];
        private int _sourceOffset;
        private int _sourceLength;
        private int _arrayDepth;
        private bool _started;
        private bool _inString;
        private bool _escaped;
        private bool _completed;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int written = 0;
            while (written < buffer.Length && !_completed)
            {
                if (_sourceOffset == _sourceLength)
                {
                    _sourceLength = await source.ReadAsync(_sourceBuffer, cancellationToken);
                    _sourceOffset = 0;
                    if (_sourceLength == 0)
                    {
                        break;
                    }
                }
                byte value = _sourceBuffer[_sourceOffset++];
                if (!_started)
                {
                    if (value != (byte)'[')
                    {
                        throw new InvalidDataException("Embedded JSON array does not start with an array.");
                    }
                    _started = true;
                    _arrayDepth = 1;
                }
                else
                {
                    Track(value);
                }
                buffer.Span[written++] = value;
            }
            return written;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Track(byte value)
        {
            if (_inString)
            {
                if (_escaped)
                {
                    _escaped = false;
                }
                else if (value == (byte)'\\')
                {
                    _escaped = true;
                }
                else if (value == (byte)'"')
                {
                    _inString = false;
                }
                return;
            }
            if (value == (byte)'"')
            {
                _inString = true;
            }
            else if (value == (byte)'[')
            {
                _arrayDepth++;
            }
            else if (value == (byte)']' && --_arrayDepth == 0)
            {
                _completed = true;
            }
        }
    }
}
