// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Server.Settings
{
    public class CottonSettings
    {
        public int MaxChunkSizeBytes { get; set; }
        public string? MasterEncryptionKey { get; set; }
        public int MasterEncryptionKeyId { get; set; }
        public int? EncryptionThreads { get; set; }
        public int CipherChunkSizeBytes { get; set; }
    }
}
