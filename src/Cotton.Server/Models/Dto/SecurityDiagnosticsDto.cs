// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class SecurityDiagnosticsDto
    {
        public string OperatingSystem { get; init; } = string.Empty;

        public bool IsLinux { get; init; }

        public bool IsContainer { get; init; }

        public bool IsPublicInstance { get; init; }

        public string? TrustedProxyIpAddress { get; init; }

        public int SecurityScore { get; init; }

        public int MaxSecurityScore { get; init; } = 10;

        public string MasterKeySource { get; init; } = string.Empty;

        public bool MasterKeyEnvironmentVariableWasConfigured { get; init; }

        public bool MasterKeyEnvironmentVariablePresentInProcess { get; init; }

        public string TempDirectoryPath { get; init; } = string.Empty;

        public bool TempDirectoryWritable { get; init; }

        public string? TempDirectoryError { get; init; }

        public DotNetDiagnosticsDto DotNetDiagnostics { get; init; } = new();

        public LinuxProcessSecurityDto LinuxProcess { get; init; } = new();

        public LinuxContainerSecurityDto LinuxContainer { get; init; } = new();

        public AdminTotpDiagnosticsDto AdminTotp { get; init; } = new();

        public DatabaseIntegrityDiagnosticsDto DatabaseIntegrity { get; init; } = new();

        public CpuFeatureDiagnosticsDto CpuFeatures { get; init; } = new();

        /// <summary>
        /// Security warnings raised while collecting diagnostics.
        /// </summary>
        public IReadOnlyList<SecurityDiagnosticWarningDto> Warnings { get; init; } = [];
    }
}
