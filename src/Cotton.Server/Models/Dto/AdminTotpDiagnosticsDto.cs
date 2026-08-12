// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class AdminTotpDiagnosticsDto
    {
        public int AdminCount { get; init; }

        public int AdminsWithTotp { get; init; }

        public int AdminsWithoutTotp { get; init; }
    }
}
