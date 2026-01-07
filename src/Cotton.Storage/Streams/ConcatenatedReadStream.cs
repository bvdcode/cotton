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
        private readonly IEnumerator<string> _hashes = hashes.GetEnumerator();
        private Stream? _current;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        //public Stream GetBlobStream(string[] uids)
        //{
        //    ArgumentNullException.ThrowIfNull(uids);
        //    foreach (var uid in uids)
        //    {
        //        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        //    }
        //    return new ConcatenatedReadStream(this, uids);
        //}

        private bool EnsureCurrent()
        {
            while (_current == null)
            {
                if (!_hashes.MoveNext()) return false;
                _current = storage.ReadAsync(_hashes.Current, pipelineContext).GetAwaiter().GetResult();
            }
            return true;
        }

        private async ValueTask<bool> EnsureCurrentAsync()
        {
            while (_current == null)
            {
                if (!_hashes.MoveNext())
                {
                    return false;
                }
                _current = await storage.ReadAsync(_hashes.Current, pipelineContext).ConfigureAwait(false);
            }
            return true;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!EnsureCurrent()) return 0;
            int read = _current!.Read(buffer, offset, count);
            if (read == 0)
            {
                _current.Dispose();
                _current = null;
                return Read(buffer, offset, count);
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!await EnsureCurrentAsync().ConfigureAwait(false)) return 0;
            int read = await _current!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                await _current.DisposeAsync().ConfigureAwait(false);
                _current = null;
                return await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _current?.Dispose();
                _hashes.Dispose();
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            _current?.Dispose();
            _hashes.Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
