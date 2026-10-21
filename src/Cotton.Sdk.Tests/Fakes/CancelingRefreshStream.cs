// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class CancelingRefreshStream(
        CancellationTokenSource cancellation,
        bool stallResponse) : Stream
    {
        private readonly MemoryStream _body = new(Encoding.UTF8.GetBytes(
            "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}"));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (stallResponse)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            await cancellation.CancelAsync();
            return await _body.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _body.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
