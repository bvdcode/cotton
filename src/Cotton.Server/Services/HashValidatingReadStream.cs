// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    internal class HashValidatingReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _expectedLength;
        private readonly byte[] _expectedHash;
        private readonly CancellationToken _cancellationToken;
        private readonly IncrementalHash _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _bytesRead;
        private bool _disposed;

        public HashValidatingReadStream(
            Stream inner,
            long expectedLength,
            byte[] expectedHash,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(expectedHash);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);

            if (expectedHash.Length != SHA256.HashSizeInBytes)
            {
                throw new ArgumentException("Expected hash must be a SHA-256 digest.", nameof(expectedHash));
            }

            _inner = inner;
            _expectedLength = expectedLength;
            _expectedHash = expectedHash.ToArray();
            _cancellationToken = cancellationToken;
        }

        public bool IsValidated { get; private set; }

        public override bool CanRead => !_disposed && _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _expectedLength;

        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException();
        }

        public void EnsureValidated()
        {
            if (!IsValidated)
            {
                throw new InvalidOperationException("The upload stream was not read completely.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cancellationToken.ThrowIfCancellationRequested();
            int bytesRead = _inner.Read(buffer, offset, count);
            ValidateRead(buffer.AsSpan(offset, bytesRead));
            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            _cancellationToken.ThrowIfCancellationRequested();

            CancellationToken effectiveCancellationToken = _cancellationToken.CanBeCanceled
                ? _cancellationToken
                : cancellationToken;
            int bytesRead = await _inner.ReadAsync(buffer, effectiveCancellationToken);
            ValidateRead(buffer.Span[..bytesRead]);
            return bytesRead;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        private void ValidateRead(ReadOnlySpan<byte> bytes)
        {
            if (IsValidated)
            {
                if (!bytes.IsEmpty)
                {
                    throw new InvalidOperationException("Unexpected stream length.");
                }

                return;
            }

            if (bytes.IsEmpty)
            {
                if (_bytesRead != _expectedLength)
                {
                    throw new InvalidOperationException("Unexpected stream length.");
                }

                ValidateHash();
                return;
            }

            long nextLength = checked(_bytesRead + bytes.Length);
            if (nextLength > _expectedLength)
            {
                throw new InvalidOperationException("Unexpected stream length.");
            }

            _hasher.AppendData(bytes);
            _bytesRead = nextLength;
            if (_bytesRead == _expectedLength)
            {
                ValidateHash();
            }
        }

        private void ValidateHash()
        {
            byte[] computedHash = _hasher.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(computedHash, _expectedHash))
            {
                throw new InvalidDataException("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            IsValidated = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _hasher.Dispose();
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override void Flush()
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
            throw new NotSupportedException();
        }
    }
}
