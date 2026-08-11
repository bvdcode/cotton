// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;

namespace Cotton.Crypto.Tests.TestUtils
{
    internal static class StreamHeaderReader
    {
        public static Task<FileHeader> ReadFileAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
            => AesGcmStreamFormat.ReadFileHeaderAsync(
                stream,
                AesGcmStreamCipher.NonceSize,
                AesGcmStreamCipher.TagSize,
                AesGcmStreamCipher.KeySize,
                cancellationToken);

        public static Task<ChunkHeader> ReadChunkAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
            => AesGcmStreamFormat.ReadChunkHeaderAsync(
                stream,
                AesGcmStreamCipher.TagSize,
                cancellationToken);

        public static Task<ChunkHeader?> TryReadChunkAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
            => AesGcmStreamFormat.TryReadChunkHeaderAsync(
                stream,
                AesGcmStreamCipher.TagSize,
                cancellationToken);
    }
}
