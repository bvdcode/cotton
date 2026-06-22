// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Internals
{
    // Work queue and results for encryption pipeline
    internal readonly struct EncryptionJob(long index, byte[] dataBuffer, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] DataBuffer { get; } = dataBuffer;
        public int DataLength { get; } = dataLength;
    }
}
