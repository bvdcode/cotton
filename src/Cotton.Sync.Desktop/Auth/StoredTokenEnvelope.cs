// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth
{
    internal sealed class StoredTokenEnvelope
    {
        public string Scheme { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }
}
