// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Shared
{
    public record CottonEncryptionSettings
    {
        public string Pepper { get; set; } = string.Empty;
        public string MasterEncryptionKey { get; set; } = string.Empty;
        public int MasterEncryptionKeyId { get; set; }
    }
}
