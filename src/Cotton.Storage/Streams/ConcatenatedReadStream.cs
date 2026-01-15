// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Streams
{
    internal class ConcatenatedReadStream(
        IStoragePipeline storage,
        IEnumerable<string> hashes,
        PipelineContext? pipelineContext = null) : Stream
    {
        private readonly List<string> _hashes = [.. hashes];
        private readonly bool _canSeek = pipelineContext?.ChunkLengths != null;
        private Stream? _current;
        private int _currentChunkIndex = -1;
        private long _position;
        private long _currentChunkPosition;

        public override bool CanRead => true;
        public override bool CanSeek => _canSeek;
        public override bool CanWrite => false;
        public override long Length => pipelineContext?.FileSizeBytes ?? throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        private (int chunkIndex, long offsetInChunk) GetChunkAtPosition(long position)
        {
            long currentPosition = 0;
            for (int i = 0; i < _hashes.Count; i++)
            {
                if (pipelineContext!.ChunkLengths!.TryGetValue(_hashes[i], out long length))
                {
                    if (currentPosition + length > position)
                    {
                        return (i, position - currentPosition);
                    }
                    currentPosition += length;
                }
            }
            return (_hashes.Count, 0);
        }

        private bool EnsureCurrentChunk(int targetChunkIndex, long requiredOffset)
        {
            if (targetChunkIndex >= _hashes.Count)
            {
                return false;
            }

            if (_currentChunkIndex != targetChunkIndex || _current == null)
            {
                _current?.Dispose();
                _current = storage.ReadAsync(_hashes[targetChunkIndex], pipelineContext).GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("Storage pipeline returned null stream.");

                _currentChunkIndex = targetChunkIndex;
                _currentChunkPosition = 0;
            }

            if (_currentChunkPosition == requiredOffset)
            {
                return true;
            }

            if (_currentChunkPosition < requiredOffset)
            {
                long toSkip = requiredOffset - _currentChunkPosition;
                SkipBytes(_current, toSkip);
                _currentChunkPosition = requiredOffset;
                return true;
            }

            // _currentChunkPosition > requiredOffset : reopen same chunk and skip from start
            _current.Dispose();
            _current = storage.ReadAsync(_hashes[targetChunkIndex], pipelineContext).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Storage pipeline returned null stream.");
            _currentChunkPosition = 0;

            if (requiredOffset > 0)
            {
                SkipBytes(_current, requiredOffset);
                _currentChunkPosition = requiredOffset;
            }

            return true;
        }

        private async ValueTask<bool> EnsureCurrentChunkAsync(int targetChunkIndex, long requiredOffset)
        {
            if (targetChunkIndex >= _hashes.Count)
            {
                return false;
            }

            if (_currentChunkIndex != targetChunkIndex || _current == null)
            {
                if (_current != null)
                {
                    await _current.DisposeAsync().ConfigureAwait(false);
                }

                _current = await storage.ReadAsync(_hashes[targetChunkIndex], pipelineContext).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Storage pipeline returned null stream.");

                _currentChunkIndex = targetChunkIndex;
                _currentChunkPosition = 0;
            }

            if (_currentChunkPosition == requiredOffset)
            {
                return true;
            }

            if (_currentChunkPosition < requiredOffset)
            {
                long toSkip = requiredOffset - _currentChunkPosition;
                await SkipBytesAsync(_current, toSkip).ConfigureAwait(false);
                _currentChunkPosition = requiredOffset;
                return true;
            }

            // _currentChunkPosition > requiredOffset : reopen same chunk and skip from start
            await _current.DisposeAsync().ConfigureAwait(false);
            _current = await storage.ReadAsync(_hashes[targetChunkIndex], pipelineContext).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Storage pipeline returned null stream.");
            _currentChunkPosition = 0;

            if (requiredOffset > 0)
            {
                await SkipBytesAsync(_current, requiredOffset).ConfigureAwait(false);
                _currentChunkPosition = requiredOffset;
            }

            return true;
        }

        private static void SkipBytes(Stream stream, long count)
        {
            if (count <= 0)
            {
                return;
            }

            byte[] buffer = new byte[(int)Math.Min(81920, count)];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = stream.Read(buffer, 0, toRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while skipping bytes");
                }
                remaining -= read;
            }
        }

        private static async ValueTask SkipBytesAsync(Stream stream, long count)
        {
            if (count <= 0)
            {
                return;
            }

            byte[] buffer = new byte[(int)Math.Min(81920, count)];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = await stream.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while skipping bytes");
                }
                remaining -= read;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_canSeek)
            {
                return ReadSequential(buffer, offset, count);
            }

            var (chunkIndex, offsetInChunk) = GetChunkAtPosition(_position);
            if (!EnsureCurrentChunk(chunkIndex, offsetInChunk))
            {
                return 0;
            }

            int totalRead = 0;
            while (count > 0 && chunkIndex < _hashes.Count)
            {
                int read = _current!.Read(buffer, offset, count);
                if (read == 0)
                {
                    chunkIndex++;
                    if (!EnsureCurrentChunk(chunkIndex, 0))
                    {
                        break;
                    }
                    continue;
                }

                totalRead += read;
                offset += read;
                count -= read;
                _position += read;
                _currentChunkPosition += read;
            }

            return totalRead;
        }

        private int ReadSequential(byte[] buffer, int offset, int count)
        {
            if (_currentChunkIndex < 0)
            {
                _currentChunkIndex = 0;
                _currentChunkPosition = 0;
                if (_hashes.Count == 0)
                {
                    return 0;
                }

                _current = storage.ReadAsync(_hashes[0], pipelineContext).GetAwaiter().GetResult();
            }

            if (_current == null)
            {
                return 0;
            }

            int read = _current.Read(buffer, offset, count);
            if (read == 0)
            {
                _current.Dispose();
                _current = null;
                _currentChunkIndex++;
                _currentChunkPosition = 0;
                if (_currentChunkIndex >= _hashes.Count)
                {
                    return 0;
                }

                _current = storage.ReadAsync(_hashes[_currentChunkIndex], pipelineContext).GetAwaiter().GetResult();
                return ReadSequential(buffer, offset, count);
            }

            _position += read;
            _currentChunkPosition += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_canSeek)
            {
                return await ReadSequentialAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            var (chunkIndex, offsetInChunk) = GetChunkAtPosition(_position);
            if (!await EnsureCurrentChunkAsync(chunkIndex, offsetInChunk).ConfigureAwait(false))
            {
                return 0;
            }

            int totalRead = 0;
            while (buffer.Length > 0 && chunkIndex < _hashes.Count)
            {
                int read = await _current!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    chunkIndex++;
                    if (!await EnsureCurrentChunkAsync(chunkIndex, 0).ConfigureAwait(false))
                    {
                        break;
                    }
                    continue;
                }

                totalRead += read;
                buffer = buffer[read..];
                _position += read;
                _currentChunkPosition += read;
            }

            return totalRead;
        }

        private async ValueTask<int> ReadSequentialAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_currentChunkIndex < 0)
            {
                _currentChunkIndex = 0;
                _currentChunkPosition = 0;
                if (_hashes.Count == 0)
                {
                    return 0;
                }

                _current = await storage.ReadAsync(_hashes[0], pipelineContext).ConfigureAwait(false);
            }

            if (_current == null)
            {
                return 0;
            }

            int read = await _current.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                await _current.DisposeAsync().ConfigureAwait(false);
                _current = null;
                _currentChunkIndex++;
                _currentChunkPosition = 0;
                if (_currentChunkIndex >= _hashes.Count)
                {
                    return 0;
                }

                _current = await storage.ReadAsync(_hashes[_currentChunkIndex], pipelineContext).ConfigureAwait(false);
                return await ReadSequentialAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            _position += read;
            _currentChunkPosition += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!_canSeek)
            {
                throw new NotSupportedException();
            }

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0)
            {
                throw new IOException("Cannot seek before the beginning of the stream");
            }

            if (newPosition > Length)
            {
                throw new IOException("Cannot seek past the end of the stream");
            }

            _position = newPosition;
            return _position;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _current?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_current is IAsyncDisposable ad)
            {
                await ad.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                _current?.Dispose();
            }

            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
