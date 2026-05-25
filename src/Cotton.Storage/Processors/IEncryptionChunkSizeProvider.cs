// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Processors
{
    /// <summary>
    /// Provides the AES-GCM plaintext chunk size used for newly written storage blobs.
    /// </summary>
    public interface IEncryptionChunkSizeProvider
    {
        /// <summary>
        /// Gets the plaintext chunk size in bytes.
        /// </summary>
        int ChunkSizeBytes { get; }
    }
}
