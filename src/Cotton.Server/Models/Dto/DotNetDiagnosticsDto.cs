// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// .NET runtime diagnostics posture (whether diagnostic ports and tracing are disabled).
    /// </summary>
    public class DotNetDiagnosticsDto
    {
        /// <summary>
        /// Whether .NET runtime diagnostics (diagnostic IPC port, EventPipe) are disabled.
        /// </summary>
        public bool Disabled { get; init; }

        /// <summary>
        /// Value of the DOTNET_EnableDiagnostics environment variable, or null if unset.
        /// </summary>
        public string? DotNetEnableDiagnostics { get; init; }

        /// <summary>
        /// Value of the legacy COMPlus_EnableDiagnostics environment variable, or null if unset.
        /// </summary>
        public string? ComPlusEnableDiagnostics { get; init; }
    }
}
