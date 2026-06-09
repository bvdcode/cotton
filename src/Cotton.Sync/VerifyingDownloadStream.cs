// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Sync
{
    internal class VerifyingDownloadStream : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly Stream _inner;
        private long _bytesWritten;
        private bool _verified;

        public VerifyingDownloadStream(Stream inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _inner.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Append(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
            Append(buffer);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            Append(buffer.Span);
        }

        public void Verify(string? expectedContentHash, long expectedSizeBytes, string relativePath)
        {
            if (expectedSizeBytes >= 0 && _bytesWritten != expectedSizeBytes)
            {
                throw new InvalidDataException(
                    "Downloaded file "
                    + relativePath
                    + " size did not match the remote manifest.");
            }

            if (!string.IsNullOrWhiteSpace(expectedContentHash))
            {
                string actualContentHash = Convert.ToHexStringLower(_hash.GetHashAndReset());
                if (!string.Equals(actualContentHash, expectedContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Downloaded file "
                        + relativePath
                        + " content hash did not match the remote manifest.");
                }
            }

            _verified = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            _hash.Dispose();
            return default;
        }

        private void Append(ReadOnlySpan<byte> buffer)
        {
            if (_verified)
            {
                throw new InvalidOperationException("Cannot write to a verified download stream.");
            }

            _hash.AppendData(buffer);
            _bytesWritten += buffer.Length;
        }
    }
}
