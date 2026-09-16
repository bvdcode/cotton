// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class VectorExtensionStatusDto
    {
        public bool ExtensionEnabled { get; init; }

        public long VectorCount { get; init; }
    }
}
