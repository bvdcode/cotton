// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Common
{
    internal class InterruptedArchiveStream(long limit) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Length + buffer.Length > limit)
            {
                throw new IOException("Download interrupted.");
            }
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
