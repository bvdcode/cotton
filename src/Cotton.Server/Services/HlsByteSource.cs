// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.IO;

namespace Cotton.Server.Services
{
    internal class HlsByteSource(Stream stream) : IRandomAccessByteSource
    {
        private const int BufferSize = 256 * 1024;
        private readonly BufferedStream _input = new(stream, BufferSize);
        private long _position;

        public long? Length => _input.Length;

        public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            int read = await ReadAtAsync(_position, destination, cancellationToken);
            _position += read;
            return read;
        }

        public ValueTask<int> ReadAtAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            cancellationToken.ThrowIfCancellationRequested();
            if (offset >= _input.Length || destination.IsEmpty)
            {
                return ValueTask.FromResult(0);
            }

            if (_input.Position != offset)
            {
                _input.Position = offset;
            }

            return _input.ReadAsync(destination, cancellationToken);
        }

        public ValueTask DisposeAsync() => _input.DisposeAsync();
    }
}
