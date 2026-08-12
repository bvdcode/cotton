// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class DotNetDiagnosticsDto
    {
        public bool Disabled { get; init; }

        public string? DotNetEnableDiagnostics { get; init; }

        public string? ComPlusEnableDiagnostics { get; init; }
    }
}
