// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates security diagnostics.
    /// </summary>
    public sealed class SecurityDiagnosticsService(
        CottonDbContext dbContext,
        ProcessHardeningStatus hardeningStatus,
        MasterKeyRuntimeState masterKeyRuntimeState,
        DatabaseIntegrityDiagnosticsService databaseIntegrityDiagnostics,
        KeyringRuntimeState keyringRuntimeState,
        KeyringDiagnosticsService keyringDiagnostics)
    {
        /// <summary>
        /// Gets snapshot async.
        /// </summary>
        public async Task<SecurityDiagnosticsDto> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            LinuxProcStatus procStatus = LinuxProcessHardening.SnapshotProcStatus();
            uint? effectiveUserId = LinuxProcessHardening.TryGetEffectiveUserId();
            int? dumpable = LinuxProcessHardening.TryGetDumpable() ?? hardeningStatus.DumpableAfter;
            string? dotnetEnableDiagnostics = Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics");
            string? comPlusEnableDiagnostics = Environment.GetEnvironmentVariable("COMPlus_EnableDiagnostics");
            bool dotnetDiagnosticsDisabled = IsZero(dotnetEnableDiagnostics) || IsZero(comPlusEnableDiagnostics);
            bool isContainer = IsContainer();
            bool isPublicInstance = Constants.IsPublicInstance;
            LinuxContainerSecuritySnapshot containerSecurity = LinuxContainerSecurity.Snapshot(isContainer);
            AdminTotpDiagnosticsDto adminTotp = await GetAdminTotpDiagnosticsAsync(cancellationToken);
            DatabaseIntegrityDiagnosticsDto databaseIntegrity = await databaseIntegrityDiagnostics
                .GetSnapshotAsync(cancellationToken);
            KeyringDiagnosticsDto keyring = await GetKeyringDiagnosticsAsync(cancellationToken);

            var linuxProcess = new LinuxProcessSecurityDto
            {
                HardeningRequested = hardeningStatus.Requested,
                HardeningApplied = hardeningStatus.Applied,
                HardeningError = hardeningStatus.Error,
                Dumpable = dumpable,
                EffectiveUserId = effectiveUserId,
                RunningAsRoot = effectiveUserId.HasValue ? effectiveUserId.Value == 0 : null,
                NoNewPrivileges = procStatus.NoNewPrivileges,
                SeccompMode = procStatus.SeccompMode,
                SeccompFilters = procStatus.SeccompFilters,
                EffectiveCapabilitiesHex = procStatus.EffectiveCapabilitiesHex,
                HasSysPtraceCapability = procStatus.HasSysPtraceCapability,
            };

            var dotnetDiagnostics = new DotNetDiagnosticsDto
            {
                Disabled = dotnetDiagnosticsDisabled,
                DotNetEnableDiagnostics = dotnetEnableDiagnostics,
                ComPlusEnableDiagnostics = comPlusEnableDiagnostics,
            };

            var linuxContainer = new LinuxContainerSecurityDto
            {
                RootFilesystemReadOnly = containerSecurity.RootFilesystemReadOnly,
                DockerSocketMounted = containerSecurity.DockerSocketMounted,
                HostPidNamespaceLikely = containerSecurity.HostPidNamespaceLikely,
                ProcOneCommandLine = containerSecurity.ProcOneCommandLine,
                CoreDumpSoftLimit = containerSecurity.CoreDumpSoftLimit,
                CoreDumpHardLimit = containerSecurity.CoreDumpHardLimit,
                CoreDumpSoftLimitDisabled = containerSecurity.CoreDumpSoftLimitDisabled,
                CorePattern = containerSecurity.CorePattern,
                AppArmorProfile = containerSecurity.AppArmorProfile,
                SelinuxContext = containerSecurity.SelinuxContext,
                SelinuxEnforcing = containerSecurity.SelinuxEnforcing,
            };

            IReadOnlyList<SecurityDiagnosticWarningDto> warnings = BuildWarnings(
                isContainer,
                isPublicInstance,
                masterKeyRuntimeState,
                dotnetDiagnostics,
                linuxProcess,
                linuxContainer,
                adminTotp,
                databaseIntegrity,
                keyring);

            return new SecurityDiagnosticsDto
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                IsLinux = OperatingSystem.IsLinux(),
                IsContainer = isContainer,
                MasterKeySource = masterKeyRuntimeState.Source,
                IsPublicInstance = isPublicInstance,
                MasterKeyEnvironmentVariableWasConfigured = masterKeyRuntimeState.EnvironmentVariableWasConfigured,
                MasterKeyEnvironmentVariablePresentInProcess = masterKeyRuntimeState.EnvironmentVariablePresentAfterResolution,
                DotNetDiagnostics = dotnetDiagnostics,
                LinuxProcess = linuxProcess,
                LinuxContainer = linuxContainer,
                AdminTotp = adminTotp,
                DatabaseIntegrity = databaseIntegrity,
                Keyring = keyring,
                SecurityScore = CalculateSecurityScore(warnings),
                Warnings = warnings,
            };
        }

        private async Task<AdminTotpDiagnosticsDto> GetAdminTotpDiagnosticsAsync(CancellationToken cancellationToken)
        {
            int adminCount = await dbContext.Users
                .CountAsync(user => user.Role == UserRole.Admin, cancellationToken);
            int adminsWithTotp = await dbContext.Users
                .CountAsync(user => user.Role == UserRole.Admin && user.IsTotpEnabled, cancellationToken);

            return new AdminTotpDiagnosticsDto
            {
                AdminCount = adminCount,
                AdminsWithTotp = adminsWithTotp,
                AdminsWithoutTotp = adminCount - adminsWithTotp,
            };
        }

        private async Task<KeyringDiagnosticsDto> GetKeyringDiagnosticsAsync(CancellationToken cancellationToken)
        {
            bool enabled = KeyringStartup.IsEnabled();
            KeyringBootstrapResult? runtime = keyringRuntimeState.Current;
            KeyringDiagnosticsSnapshot? snapshot = null;
            var warnings = new List<string>();

            try
            {
                snapshot = await keyringDiagnostics.GetSnapshotAsync(cancellationToken: cancellationToken);
                warnings.AddRange(snapshot.Warnings);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add("keyring-diagnostics-failed");
            }

            int? legacyDecryptOnlyKeyCount = runtime is null
                ? snapshot?.LegacyDecryptOnlyKeyCount
                : runtime.State.Keys.Count(x =>
                    x.Origin == KeyringKeyOrigin.LegacyV1MasterDerived
                    && x.Status is KeyringKeyStatus.DecryptOnly or KeyringKeyStatus.VerifyOnly);
            int? recoverySlotCount = runtime is null
                ? snapshot?.RecoverySlotCount
                : runtime.AccessEnvelope.Recipients.Count(x => x.Type == KeyringCryptography.RecoverySlotType);

            return new KeyringDiagnosticsDto
            {
                Enabled = enabled,
                Loaded = runtime is not null,
                AccessEnvelopePresent = snapshot?.AccessEnvelopePresent ?? false,
                StateSnapshotPresent = snapshot?.StateSnapshotPresent ?? false,
                AccessGeneration = snapshot?.AccessGeneration ?? runtime?.AccessEnvelope.Generation,
                StateGeneration = snapshot?.StateGeneration ?? runtime?.State.StateGeneration,
                RootEpoch = snapshot?.RootEpoch ?? runtime?.State.RootEpoch,
                KeyCount = runtime?.State.Keys.Count ?? snapshot?.KeyCount,
                RecoverySlotCount = recoverySlotCount,
                LegacyDecryptOnlyKeyCount = legacyDecryptOnlyKeyCount,
                Warnings = warnings,
            };
        }

        private static IReadOnlyList<SecurityDiagnosticWarningDto> BuildWarnings(
            bool isContainer,
            bool isPublicInstance,
            MasterKeyRuntimeState masterKey,
            DotNetDiagnosticsDto dotnetDiagnostics,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer,
            AdminTotpDiagnosticsDto adminTotp,
            DatabaseIntegrityDiagnosticsDto databaseIntegrity,
            KeyringDiagnosticsDto keyring)
        {
            var warnings = new List<SecurityDiagnosticWarningDto>();
            AddPublicInstanceWarning(warnings, isPublicInstance);
            AddMasterKeyWarning(warnings, masterKey);
            AddAdminTotpWarning(warnings, adminTotp);
            AddDotNetDiagnosticsWarning(warnings, dotnetDiagnostics);
            AddLinuxProcessWarnings(warnings, isContainer, linuxProcess);
            AddLinuxContainerWarnings(warnings, isContainer, linuxProcess, linuxContainer);
            AddHardeningWarning(warnings, linuxProcess);
            AddDatabaseIntegrityWarnings(warnings, databaseIntegrity);
            AddKeyringWarnings(warnings, keyring);
            return warnings;
        }

        private static void AddKeyringWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            KeyringDiagnosticsDto keyring)
        {
            if (!keyring.Enabled)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-v2-disabled",
                    Severity = "info",
                    Message = "Keyring v2 is not enabled for this process. New random data keys, replicated keyring state, and unlock-key rotation are not active.",
                });
                return;
            }

            bool diagnosticsFailed = keyring.Warnings.Contains("keyring-diagnostics-failed", StringComparer.Ordinal);
            if (diagnosticsFailed)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-diagnostics-failed",
                    Severity = "warning",
                    Message = "Keyring v2 is enabled, but Cotton could not scan the replicated keyring objects for this checkup.",
                });
            }

            if (!keyring.Loaded)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-not-loaded",
                    Severity = "critical",
                    Message = "Keyring v2 is enabled, but no keyring is loaded in the current process. New encryption should not proceed until startup bootstrap succeeds.",
                });
            }

            if (!diagnosticsFailed && (!keyring.AccessEnvelopePresent || !keyring.StateSnapshotPresent))
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-replicas-incomplete",
                    Severity = "critical",
                    Message = "Keyring v2 is enabled, but the replicated access envelope or state snapshot is missing from the scanned replicas.",
                });
            }

            if (keyring.LegacyDecryptOnlyKeyCount.GetValueOrDefault() > 0)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-legacy-debt",
                    Severity = "warning",
                    Message = "This keyring still contains legacy decrypt-only keys. Existing legacy chunks remain compatible, but they need re-encryption before the old master key stops being useful for those chunks.",
                });
            }

            if (keyring.Loaded && keyring.RecoverySlotCount.GetValueOrDefault() == 0)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "keyring-recovery-missing",
                    Severity = "warning",
                    Message = "This keyring has no recovery phrase recipient slot. Create a recovery phrase and export a fresh recovery kit before relying on keyring recovery.",
                });
            }
        }

        private static void AddDatabaseIntegrityWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            DatabaseIntegrityDiagnosticsDto databaseIntegrity)
        {
            if (databaseIntegrity.UnsignedProtectedRows > 0)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "db-integrity-unsigned-rows",
                    Severity = "critical",
                    Message = $"{databaseIntegrity.UnsignedProtectedRows} protected database rows are missing valid integrity signatures. Run the bridge release backfill before trusting this instance.",
                });
            }

            if (!databaseIntegrity.BridgeBackfillEnabled)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "db-integrity-bridge-mode",
                Severity = "warning",
                Message = "Database integrity bridge mode is enabled for this upgrade window. Existing rows are being signed automatically on startup. Disable bridge mode after the upgrade window so missing or invalid signatures become hard failures.",
            });
        }

        private static void AddPublicInstanceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isPublicInstance)
        {
            if (!isPublicInstance)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "public-instance",
                Severity = "warning",
                Message = "This instance allows public/demo account creation. Keep quotas, default content, and abuse monitoring configured before exposing it on the internet.",
            });
        }

        private static void AddMasterKeyWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            MasterKeyRuntimeState masterKey)
        {
            if (!masterKey.EnvironmentVariableWasConfigured)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "master-key-from-environment",
                Severity = "warning",
                Message = "This process was unlocked from COTTON_MASTER_KEY. Cotton clears its own process environment after reading it, but container runtimes may still expose configured environment variables through deployment metadata or docker exec environments.",
            });
        }

        private static void AddAdminTotpWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            AdminTotpDiagnosticsDto adminTotp)
        {
            if (adminTotp.AdminsWithoutTotp <= 0)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "admins-without-2fa",
                Severity = "warning",
                Message = $"{adminTotp.AdminsWithoutTotp} of {adminTotp.AdminCount} admin accounts do not have 2FA enabled.",
            });
        }

        private static void AddDotNetDiagnosticsWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            DotNetDiagnosticsDto dotnetDiagnostics)
        {
            if (dotnetDiagnostics.Disabled)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "dotnet-diagnostics-enabled",
                Severity = "warning",
                Message = "DOTNET diagnostics appear enabled. Production containers should set DOTNET_EnableDiagnostics=0 to disable debugger, profiler, EventPipe, and dump collection endpoints.",
            });
        }

        private static void AddLinuxProcessWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isContainer,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            AddDumpableWarning(warnings, linuxProcess);
            AddPtraceWarning(warnings, linuxProcess);
            AddNoNewPrivilegesWarning(warnings, isContainer, linuxProcess);
            AddSeccompWarning(warnings, linuxProcess);
            AddRootWarning(warnings, linuxProcess);
        }

        private static void AddDumpableWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.Dumpable == 0)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "process-dumpable",
                Severity = "warning",
                Message = "The Linux process is dumpable. Set COTTON_PROCESS_HARDENING=true or run the official container defaults to request PR_SET_DUMPABLE=0 early at startup.",
            });
        }

        private static void AddPtraceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.HasSysPtraceCapability != true)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "sys-ptrace-capability",
                Severity = "critical",
                Message = "CAP_SYS_PTRACE is effective for this process. Avoid SYS_PTRACE/privileged containers unless actively debugging.",
            });
        }

        private static void AddNoNewPrivilegesWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isContainer,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.NoNewPrivileges != 0)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "new-privileges-allowed",
                Severity = isContainer ? "warning" : "info",
                Message = "no-new-privileges is not enabled. In Docker Compose, security_opt: [\"no-new-privileges:true\"] is a cheap hardening layer.",
            });
        }

        private static void AddSeccompWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.SeccompMode != 0)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "seccomp-disabled",
                Severity = "warning",
                Message = "Seccomp appears disabled. Docker's default seccomp profile is a useful baseline; avoid seccomp=unconfined in production.",
            });
        }

        private static void AddRootWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.RunningAsRoot != true)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "running-as-root",
                Severity = "info",
                Message = "The process is running as root. This may be acceptable for simple self-hosting, but a dedicated non-root UID is stronger when volume permissions are prepared for it.",
            });
        }

        private static void AddLinuxContainerWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isContainer,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            if (isContainer)
            {
                AddRootFilesystemWarning(warnings, linuxContainer);
                AddDockerSocketWarning(warnings, linuxContainer);
                AddHostPidNamespaceWarning(warnings, linuxContainer);
                AddMandatoryAccessControlWarning(warnings, linuxContainer);
            }

            AddCoreDumpLimitWarning(warnings, linuxProcess, linuxContainer);
        }

        private static void AddRootFilesystemWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (linuxContainer.RootFilesystemReadOnly != false)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "root-filesystem-writable",
                Severity = "info",
                Message = "The container root filesystem is writable. Set read_only: true and mount only the required data directories as writable volumes.",
            });
        }

        private static void AddDockerSocketWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (!linuxContainer.DockerSocketMounted)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "docker-socket-mounted",
                Severity = "critical",
                Message = "The Docker socket is visible inside the Cotton container. Remove the socket mount; it is effectively host-root access from the web process.",
            });
        }

        private static void AddHostPidNamespaceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (linuxContainer.HostPidNamespaceLikely != true)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "host-pid-namespace",
                Severity = "critical",
                Message = "Cotton appears to share the host PID namespace. Remove pid: host so process isolation and procfs visibility stay inside the container boundary.",
            });
        }

        private static void AddMandatoryAccessControlWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            bool appArmorUnconfined = linuxContainer.AppArmorProfile?.StartsWith("unconfined", StringComparison.OrdinalIgnoreCase) == true;
            bool hasMacProfile = !string.IsNullOrWhiteSpace(linuxContainer.AppArmorProfile)
                || !string.IsNullOrWhiteSpace(linuxContainer.SelinuxContext);
            bool selinuxPermissive = linuxContainer.SelinuxEnforcing == false;

            if (hasMacProfile && !appArmorUnconfined && !selinuxPermissive)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "mandatory-access-control-unconfined",
                Severity = "warning",
                Message = "No enforcing AppArmor or SELinux confinement was detected for the container. Use Docker default AppArmor, a custom AppArmor profile, or an enforcing SELinux container context.",
            });
        }

        private static void AddCoreDumpLimitWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (linuxContainer.CoreDumpSoftLimitDisabled != false || linuxProcess.Dumpable == 0)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "core-dumps-enabled",
                Severity = "warning",
                Message = "Core dump limits allow dumps while the process may be dumpable. Set ulimit core=0 and keep COTTON_PROCESS_HARDENING=true so crashes cannot write memory snapshots containing secrets.",
            });
        }

        private static void AddHardeningWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (!linuxProcess.HardeningRequested || linuxProcess.HardeningApplied)
            {
                return;
            }

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "process-hardening-failed",
                Severity = "warning",
                Message = linuxProcess.HardeningError ?? "Process hardening was requested but did not apply.",
            });
        }

        private static int CalculateSecurityScore(IReadOnlyList<SecurityDiagnosticWarningDto> warnings)
        {
            int penalty = warnings.Sum(warning => warning.Severity switch
            {
                "critical" => 3,
                "warning" => 2,
                "info" => 1,
                _ => 0,
            });

            return Math.Max(0, 10 - penalty);
        }

        private static bool IsZero(string? value) => string.Equals(value, "0", StringComparison.Ordinal);

        private static bool IsContainer()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (File.Exists("/.dockerenv"))
            {
                return true;
            }

            const string cgroupPath = "/proc/1/cgroup";
            if (!File.Exists(cgroupPath))
            {
                return false;
            }

            return File.ReadLines(cgroupPath).Any(line =>
                line.Contains("docker", StringComparison.OrdinalIgnoreCase)
                || line.Contains("kubepods", StringComparison.OrdinalIgnoreCase)
                || line.Contains("containerd", StringComparison.OrdinalIgnoreCase));
        }
    }
}
