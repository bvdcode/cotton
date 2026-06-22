// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Internals
{
    internal readonly struct DecryptionResult(long index, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Data { get; } = data; // rented from pool
        public int DataLength { get; } = dataLength;
    }
}
