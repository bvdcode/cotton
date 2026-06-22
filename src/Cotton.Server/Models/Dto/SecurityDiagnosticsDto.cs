// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the security diagnostics API payload.
    /// </summary>
    public class SecurityDiagnosticsDto
    {
        /// <summary>
        /// Gets or sets operating system.
        /// </summary>
        public string OperatingSystem { get; init; } = string.Empty;
        /// <summary>
        /// Indicates whether linux.
        /// </summary>
        public bool IsLinux { get; init; }
        /// <summary>
        /// Indicates whether container.
        /// </summary>
        public bool IsContainer { get; init; }
        /// <summary>
        /// Indicates whether public instance.
        /// </summary>
        public bool IsPublicInstance { get; init; }
        /// <summary>
        /// Gets or sets security score.
        /// </summary>
        public int SecurityScore { get; init; }
        /// <summary>
        /// Gets or sets max security score.
        /// </summary>
        public int MaxSecurityScore { get; init; } = 10;
        /// <summary>
        /// Gets or sets master key source.
        /// </summary>
        public string MasterKeySource { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets master key environment variable was configured.
        /// </summary>
        public bool MasterKeyEnvironmentVariableWasConfigured { get; init; }
        /// <summary>
        /// Gets or sets master key environment variable present in process.
        /// </summary>
        public bool MasterKeyEnvironmentVariablePresentInProcess { get; init; }
        /// <summary>
        /// Gets OS temp directory path.
        /// </summary>
        public string TempDirectoryPath { get; init; } = string.Empty;
        /// <summary>
        /// Indicates whether OS temp directory is writable.
        /// </summary>
        public bool TempDirectoryWritable { get; init; }
        /// <summary>
        /// Gets OS temp directory write error.
        /// </summary>
        public string? TempDirectoryError { get; init; }
        /// <summary>
        /// Gets or sets dot net diagnostics.
        /// </summary>
        public DotNetDiagnosticsDto DotNetDiagnostics { get; init; } = new();
        /// <summary>
        /// Gets or sets linux process.
        /// </summary>
        public LinuxProcessSecurityDto LinuxProcess { get; init; } = new();
        /// <summary>
        /// Gets Linux container boundary diagnostics.
        /// </summary>
        public LinuxContainerSecurityDto LinuxContainer { get; init; } = new();
        /// <summary>
        /// Gets or sets admin totp.
        /// </summary>
        public AdminTotpDiagnosticsDto AdminTotp { get; init; } = new();
        /// <summary>
        /// Gets or sets database integrity.
        /// </summary>
        public DatabaseIntegrityDiagnosticsDto DatabaseIntegrity { get; init; } = new();
        /// <summary>
        /// Gets CPU crypto and memory-encryption feature diagnostics.
        /// </summary>
        public CpuFeatureDiagnosticsDto CpuFeatures { get; init; } = new();
        /// <summary>
        /// Gets or sets warnings.
        /// </summary>
        public IReadOnlyList<SecurityDiagnosticWarningDto> Warnings { get; init; } = [];
    }
}
