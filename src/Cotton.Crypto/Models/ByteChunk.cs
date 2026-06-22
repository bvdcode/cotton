// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;

namespace Cotton.Crypto.Models
{
    /// <summary>
    /// Zero-copy chunk of bytes for channel pipelines; the receiver returns the buffer to the shared pool.
    /// </summary>
    /// <param name="buffer">Backing byte array.</param>
    /// <param name="length">Number of valid bytes in <paramref name="buffer"/>.</param>
    public readonly struct ByteChunk(byte[] buffer, int length)
    {
        /// <summary>
        /// The backing byte array.
        /// </summary>
        public byte[] Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));

        /// <summary>
        /// Number of valid bytes in <see cref="Buffer"/>.
        /// </summary>
        public int Length { get; } = length;
    }
}
