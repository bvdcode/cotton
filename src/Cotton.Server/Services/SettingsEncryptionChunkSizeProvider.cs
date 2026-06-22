// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using Cotton.Storage.Processors;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Reads the runtime AES-GCM plaintext chunk size from server settings.
    /// </summary>
    public sealed class SettingsEncryptionChunkSizeProvider(SettingsProvider settings) : IEncryptionChunkSizeProvider
    {
        /// <inheritdoc />
        public int ChunkSizeBytes => settings.GetServerSettings().CipherChunkSizeBytes;
    }
}
