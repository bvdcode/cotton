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

    /// <summary>
    /// Represents the linux process security API payload.
    /// </summary>
    public class LinuxProcessSecurityDto
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
    /// Represents Linux container boundary diagnostics.
    /// </summary>
    public class LinuxContainerSecurityDto
    {
        /// <summary>
        /// Indicates whether the container root filesystem is mounted read-only.
        /// </summary>
        public bool? RootFilesystemReadOnly { get; init; }
        /// <summary>
        /// Indicates whether a Docker daemon socket is visible inside the process mount namespace.
        /// </summary>
        public bool DockerSocketMounted { get; init; }
        /// <summary>
        /// Indicates whether the container appears to share the host PID namespace.
        /// </summary>
        public bool? HostPidNamespaceLikely { get; init; }
        /// <summary>
        /// Gets the visible command line for PID 1.
        /// </summary>
        public string? ProcOneCommandLine { get; init; }
        /// <summary>
        /// Gets the soft core dump size limit from procfs.
        /// </summary>
        public string? CoreDumpSoftLimit { get; init; }
        /// <summary>
        /// Gets the hard core dump size limit from procfs.
        /// </summary>
        public string? CoreDumpHardLimit { get; init; }
        /// <summary>
        /// Indicates whether the soft core dump limit disables core dumps.
        /// </summary>
        public bool? CoreDumpSoftLimitDisabled { get; init; }
        /// <summary>
        /// Gets the kernel core dump pattern.
        /// </summary>
        public string? CorePattern { get; init; }
        /// <summary>
        /// Gets the active AppArmor profile, when AppArmor is the visible LSM.
        /// </summary>
        public string? AppArmorProfile { get; init; }
        /// <summary>
        /// Gets the active SELinux context, when SELinux is the visible LSM.
        /// </summary>
        public string? SelinuxContext { get; init; }
        /// <summary>
        /// Indicates whether SELinux is enforcing.
        /// </summary>
        public bool? SelinuxEnforcing { get; init; }
    }

    /// <summary>
    /// Represents the admin totp diagnostics API payload.
    /// </summary>
    public class AdminTotpDiagnosticsDto
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
    public class DatabaseIntegrityDiagnosticsDto
    {
        /// <summary>
        /// Gets a value indicating whether database integrity protection is enabled.
        /// </summary>
        public bool Enabled { get; init; }
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
    /// Represents CPU feature diagnostics.
    /// </summary>
    public class CpuFeatureDiagnosticsDto
    {
        /// <summary>
        /// Gets process architecture.
        /// </summary>
        public string Architecture { get; init; } = string.Empty;
        /// <summary>
        /// Gets OS architecture.
        /// </summary>
        public string OsArchitecture { get; init; } = string.Empty;
        /// <summary>
        /// Gets logical processor count visible to the process.
        /// </summary>
        public int LogicalProcessorCount { get; init; }
        /// <summary>
        /// Gets CPU vendor ID reported by Linux procfs.
        /// </summary>
        public string? VendorId { get; init; }
        /// <summary>
        /// Gets CPU model name reported by Linux procfs.
        /// </summary>
        public string? ModelName { get; init; }
        /// <summary>
        /// Indicates whether AES-GCM hardware acceleration is likely available to the runtime.
        /// </summary>
        public bool AesGcmHardwareAccelerationLikely { get; init; }
        /// <summary>
        /// Gets AES-NI feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto AesNi { get; init; } = new();
        /// <summary>
        /// Gets PCLMULQDQ feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Pclmulqdq { get; init; } = new();
        /// <summary>
        /// Gets VAES feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Vaes { get; init; } = new();
        /// <summary>
        /// Gets VPCLMULQDQ feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Vpclmulqdq { get; init; } = new();
        /// <summary>
        /// Gets AVX2 feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Avx2 { get; init; } = new();
        /// <summary>
        /// Gets total memory encryption feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto Tme { get; init; } = new();
        /// <summary>
        /// Gets multi-key total memory encryption feature availability.
        /// </summary>
        public CpuFeatureAvailabilityDto TmeMk { get; init; } = new();
        /// <summary>
        /// Gets PCONFIG feature availability used by some TME-MK platforms.
        /// </summary>
        public CpuFeatureAvailabilityDto Pconfig { get; init; } = new();
        /// <summary>
        /// Gets raw Linux CPU flags visible through procfs.
        /// </summary>
        public IReadOnlyList<string> LinuxCpuFlags { get; init; } = [];
    }

    /// <summary>
    /// Represents availability of a single CPU feature.
    /// </summary>
    public class CpuFeatureAvailabilityDto
    {
        /// <summary>
        /// Gets the runtime intrinsic support status, when applicable.
        /// </summary>
        public bool? RuntimeSupported { get; init; }
        /// <summary>
        /// Gets Linux procfs flag presence, when available.
        /// </summary>
        public bool? LinuxFlagPresent { get; init; }
    }

    /// <summary>
    /// Represents the security diagnostic warning API payload.
    /// </summary>
    public class SecurityDiagnosticWarningDto
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
