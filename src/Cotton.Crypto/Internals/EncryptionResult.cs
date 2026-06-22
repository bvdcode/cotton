// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Internals
{
    internal readonly struct EncryptionResult(long index, Tag128 tag, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public Tag128 Tag { get; } = tag; // 16-byte tag, value type
        public byte[] Data { get; } = data; // ciphertext buffer, rented from pool
        public int DataLength { get; } = dataLength;
    }
}
