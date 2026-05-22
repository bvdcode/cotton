// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public sealed class SecurityDiagnosticsDto
    {
        public string OperatingSystem { get; init; } = string.Empty;
        public bool IsLinux { get; init; }
        public bool IsContainer { get; init; }
        public bool IsPublicInstance { get; init; }
        public int SecurityScore { get; init; }
        public int MaxSecurityScore { get; init; } = 10;
        public string MasterKeySource { get; init; } = string.Empty;
        public bool MasterKeyEnvironmentVariableWasConfigured { get; init; }
        public bool MasterKeyEnvironmentVariablePresentInProcess { get; init; }
        public DotNetDiagnosticsDto DotNetDiagnostics { get; init; } = new();
        public LinuxProcessSecurityDto LinuxProcess { get; init; } = new();
        public AdminTotpDiagnosticsDto AdminTotp { get; init; } = new();
        public DatabaseIntegrityDiagnosticsDto DatabaseIntegrity { get; init; } = new();
        public IReadOnlyList<SecurityDiagnosticWarningDto> Warnings { get; init; } = [];
    }

    public sealed class DotNetDiagnosticsDto
    {
        public bool Disabled { get; init; }
        public string? DotNetEnableDiagnostics { get; init; }
        public string? ComPlusEnableDiagnostics { get; init; }
    }

    public sealed class LinuxProcessSecurityDto
    {
        public bool HardeningRequested { get; init; }
        public bool HardeningApplied { get; init; }
        public string? HardeningError { get; init; }
        public int? Dumpable { get; init; }
        public uint? EffectiveUserId { get; init; }
        public bool? RunningAsRoot { get; init; }
        public int? NoNewPrivileges { get; init; }
        public int? SeccompMode { get; init; }
        public int? SeccompFilters { get; init; }
        public string? EffectiveCapabilitiesHex { get; init; }
        public bool? HasSysPtraceCapability { get; init; }
    }

    public sealed class AdminTotpDiagnosticsDto
    {
        public int AdminCount { get; init; }
        public int AdminsWithTotp { get; init; }
        public int AdminsWithoutTotp { get; init; }
    }

    public sealed class DatabaseIntegrityDiagnosticsDto
    {
        public bool Enabled { get; init; }
        public bool BridgeBackfillEnabled { get; init; }
        public int ProtectedEntityTypes { get; init; }
        public int UnsignedProtectedRows { get; init; }
    }

    public sealed class SecurityDiagnosticWarningDto
    {
        public string Code { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
