// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class DatabaseIntegrityDiagnosticsDto
    {
        public bool Enabled { get; init; }

        public int ProtectedEntityTypes { get; init; }

        public int UnsignedProtectedRows { get; init; }
    }
}
