// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Crypto.Models
{
    public readonly struct ByteChunk(byte[] buffer, int length)
    {
        public byte[] Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));

        public int Length { get; } = length;
    }
}
