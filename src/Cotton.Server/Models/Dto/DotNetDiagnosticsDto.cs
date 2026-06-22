// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the dot net diagnostics API payload.
    /// </summary>
    public class DotNetDiagnosticsDto
    {
        /// <summary>
        /// Gets a value indicating whether .NET runtime diagnostics are disabled.
        /// </summary>
        public bool Disabled { get; init; }
        /// <summary>
        /// Gets or sets dot net enable diagnostics.
        /// </summary>
        public string? DotNetEnableDiagnostics { get; init; }
        /// <summary>
        /// Gets or sets com plus enable diagnostics.
        /// </summary>
        public string? ComPlusEnableDiagnostics { get; init; }
    }
}
