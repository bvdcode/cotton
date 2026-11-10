// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.IO.Pipelines;

namespace Cotton.Crypto.Streams
{
    public class TransformReadStream : Stream
    {
        private readonly Stream _reader;
        private readonly Pipe _pipe;
        private readonly CancellationTokenSource _lifetime;
        private readonly Task _producer;
        private int _disposed;

        public TransformReadStream(
            Stream input,
            Func<Stream, Stream, CancellationToken, Task> transform,
            PipeOptions options,
            bool leaveInputOpen = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(transform);
            _pipe = new Pipe(options);
            _reader = _pipe.Reader.AsStream();
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _producer = Task.Run(() => ProduceAsync(input, transform, leaveInputOpen));
        }

        public override bool CanRead => Volatile.Read(ref _disposed) == 0;
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
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _reader.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _reader.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _lifetime.Cancel();
                _reader.Dispose();
                _ = _producer.ContinueWith(
                    _ => _lifetime.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            Dispose();
            await _producer.ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private async Task ProduceAsync(
            Stream input,
            Func<Stream, Stream, CancellationToken, Task> transform,
            bool leaveInputOpen)
        {
            Exception? failure = null;
            try
            {
                await using Stream writer = _pipe.Writer.AsStream(leaveOpen: true);
                await transform(input, writer, _lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                try
                {
                    if (!leaveInputOpen)
                    {
                        await input.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
                await _pipe.Writer.CompleteAsync(failure).ConfigureAwait(false);
            }
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
