// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Aggregated server security posture returned by the diagnostics API.
    /// </summary>
    public class SecurityDiagnosticsDto
    {
        /// <summary>
        /// Operating system description the server is running on.
        /// </summary>
        public string OperatingSystem { get; init; } = string.Empty;

        /// <summary>
        /// Whether the server is running on Linux.
        /// </summary>
        public bool IsLinux { get; init; }

        /// <summary>
        /// Whether the server appears to be running inside a container.
        /// </summary>
        public bool IsContainer { get; init; }

        /// <summary>
        /// Whether this instance is configured as publicly reachable.
        /// </summary>
        public bool IsPublicInstance { get; init; }

        /// <summary>
        /// Computed security score, out of <see cref="MaxSecurityScore"/>.
        /// </summary>
        public int SecurityScore { get; init; }

        /// <summary>
        /// Maximum attainable security score.
        /// </summary>
        public int MaxSecurityScore { get; init; } = 10;

        /// <summary>
        /// Where the master key was loaded from (e.g. environment variable, file, unlock server).
        /// </summary>
        public string MasterKeySource { get; init; } = string.Empty;

        /// <summary>
        /// Whether the master-key environment variable was configured at startup.
        /// </summary>
        public bool MasterKeyEnvironmentVariableWasConfigured { get; init; }

        /// <summary>
        /// Whether the master-key environment variable is still present in the running process
        /// (it should be cleared after the key is loaded).
        /// </summary>
        public bool MasterKeyEnvironmentVariablePresentInProcess { get; init; }

        /// <summary>
        /// Path of the OS temporary directory.
        /// </summary>
        public string TempDirectoryPath { get; init; } = string.Empty;

        /// <summary>
        /// Whether the OS temporary directory is writable by the process.
        /// </summary>
        public bool TempDirectoryWritable { get; init; }

        /// <summary>
        /// Error encountered while probing the temporary directory, or null if none.
        /// </summary>
        public string? TempDirectoryError { get; init; }

        /// <summary>
        /// .NET runtime diagnostics posture.
        /// </summary>
        public DotNetDiagnosticsDto DotNetDiagnostics { get; init; } = new();

        /// <summary>
        /// Linux process security posture.
        /// </summary>
        public LinuxProcessSecurityDto LinuxProcess { get; init; } = new();

        /// <summary>
        /// Linux container boundary diagnostics.
        /// </summary>
        public LinuxContainerSecurityDto LinuxContainer { get; init; } = new();

        /// <summary>
        /// Administrator TOTP (two-factor) coverage.
        /// </summary>
        public AdminTotpDiagnosticsDto AdminTotp { get; init; } = new();

        /// <summary>
        /// Database integrity protection status.
        /// </summary>
        public DatabaseIntegrityDiagnosticsDto DatabaseIntegrity { get; init; } = new();

        /// <summary>
        /// CPU crypto and memory-encryption feature diagnostics.
        /// </summary>
        public CpuFeatureDiagnosticsDto CpuFeatures { get; init; } = new();

        /// <summary>
        /// Security warnings raised while collecting diagnostics.
        /// </summary>
        public IReadOnlyList<SecurityDiagnosticWarningDto> Warnings { get; init; } = [];
    }
}
