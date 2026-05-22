// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the security diagnostics API payload.
    /// </summary>
    public sealed class SecurityDiagnosticsDto
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
        /// Gets or sets dot net diagnostics.
        /// </summary>
        public DotNetDiagnosticsDto DotNetDiagnostics { get; init; } = new();
        /// <summary>
        /// Gets or sets linux process.
        /// </summary>
        public LinuxProcessSecurityDto LinuxProcess { get; init; } = new();
        /// <summary>
        /// Gets or sets admin totp.
        /// </summary>
        public AdminTotpDiagnosticsDto AdminTotp { get; init; } = new();
        /// <summary>
        /// Gets or sets database integrity.
        /// </summary>
        public DatabaseIntegrityDiagnosticsDto DatabaseIntegrity { get; init; } = new();
        /// <summary>
        /// Gets or sets warnings.
        /// </summary>
        public IReadOnlyList<SecurityDiagnosticWarningDto> Warnings { get; init; } = [];
    }

    /// <summary>
    /// Represents the dot net diagnostics API payload.
    /// </summary>
    public sealed class DotNetDiagnosticsDto
    {
        /// <summary>
        /// Disables d.
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

    /// <summary>
    /// Represents the linux process security API payload.
    /// </summary>
    public sealed class LinuxProcessSecurityDto
    {
        /// <summary>
        /// Gets or sets hardening requested.
        /// </summary>
        public bool HardeningRequested { get; init; }
        /// <summary>
        /// Gets or sets hardening applied.
        /// </summary>
        public bool HardeningApplied { get; init; }
        /// <summary>
        /// Gets or sets hardening error.
        /// </summary>
        public string? HardeningError { get; init; }
        /// <summary>
        /// Gets or sets dumpable.
        /// </summary>
        public int? Dumpable { get; init; }
        /// <summary>
        /// Gets or sets effective user id.
        /// </summary>
        public uint? EffectiveUserId { get; init; }
        /// <summary>
        /// Gets or sets running as root.
        /// </summary>
        public bool? RunningAsRoot { get; init; }
        /// <summary>
        /// Gets or sets no new privileges.
        /// </summary>
        public int? NoNewPrivileges { get; init; }
        /// <summary>
        /// Gets or sets seccomp mode.
        /// </summary>
        public int? SeccompMode { get; init; }
        /// <summary>
        /// Gets or sets seccomp filters.
        /// </summary>
        public int? SeccompFilters { get; init; }
        /// <summary>
        /// Gets or sets effective capabilities hex.
        /// </summary>
        public string? EffectiveCapabilitiesHex { get; init; }
        /// <summary>
        /// Indicates whether sys ptrace capability.
        /// </summary>
        public bool? HasSysPtraceCapability { get; init; }
    }

    /// <summary>
    /// Represents the admin totp diagnostics API payload.
    /// </summary>
    public sealed class AdminTotpDiagnosticsDto
    {
        /// <summary>
        /// Gets or sets admin count.
        /// </summary>
        public int AdminCount { get; init; }
        /// <summary>
        /// Gets or sets admins with totp.
        /// </summary>
        public int AdminsWithTotp { get; init; }
        /// <summary>
        /// Gets or sets admins without totp.
        /// </summary>
        public int AdminsWithoutTotp { get; init; }
    }

    /// <summary>
    /// Represents the database integrity diagnostics API payload.
    /// </summary>
    public sealed class DatabaseIntegrityDiagnosticsDto
    {
        /// <summary>
        /// Enables d.
        /// </summary>
        public bool Enabled { get; init; }
        /// <summary>
        /// Gets or sets bridge backfill enabled.
        /// </summary>
        public bool BridgeBackfillEnabled { get; init; }
        /// <summary>
        /// Gets or sets protected entity types.
        /// </summary>
        public int ProtectedEntityTypes { get; init; }
        /// <summary>
        /// Gets or sets unsigned protected rows.
        /// </summary>
        public int UnsignedProtectedRows { get; init; }
    }

    /// <summary>
    /// Represents the security diagnostic warning API payload.
    /// </summary>
    public sealed class SecurityDiagnosticWarningDto
    {
        /// <summary>
        /// Gets or sets code.
        /// </summary>
        public string Code { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets severity.
        /// </summary>
        public string Severity { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets message.
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
