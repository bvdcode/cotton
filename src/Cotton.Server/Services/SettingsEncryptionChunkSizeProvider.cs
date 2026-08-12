// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using Cotton.Storage.Processors;

namespace Cotton.Server.Services
{
    public class SettingsEncryptionChunkSizeProvider(SettingsProvider settings) : IEncryptionChunkSizeProvider
    {
        public int ChunkSizeBytes => settings.GetServerSettings().CipherChunkSizeBytes;
    }
}
