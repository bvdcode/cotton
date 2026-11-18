// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Tests.TestUtils
{
    internal class FragmentedReadStream(Stream inner, int readSize) : NonSeekableReadStream(inner)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, readSize));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, readSize)], cancellationToken);
        }
    }
}
