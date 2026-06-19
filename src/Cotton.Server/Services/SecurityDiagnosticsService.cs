// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
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
        TempDirectoryProbe tempDirectoryProbe)
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
            TempDirectoryProbeResult tempDirectory = tempDirectoryProbe.Probe();
            LinuxContainerSecuritySnapshot containerSecurity = LinuxContainerSecurity.Snapshot(isContainer);
            AdminTotpDiagnosticsDto adminTotp = await GetAdminTotpDiagnosticsAsync(cancellationToken);
            DatabaseIntegrityDiagnosticsDto databaseIntegrity = await databaseIntegrityDiagnostics
                .GetSnapshotAsync(cancellationToken);
            CpuFeatureDiagnosticsDto cpuFeatures = CpuFeatureDiagnostics.Snapshot();

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
                tempDirectory);

            return new SecurityDiagnosticsDto
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                IsLinux = OperatingSystem.IsLinux(),
                IsContainer = isContainer,
                MasterKeySource = masterKeyRuntimeState.Source,
                IsPublicInstance = isPublicInstance,
                MasterKeyEnvironmentVariableWasConfigured = masterKeyRuntimeState.EnvironmentVariableWasConfigured,
                MasterKeyEnvironmentVariablePresentInProcess = masterKeyRuntimeState.EnvironmentVariablePresentAfterResolution,
                TempDirectoryPath = tempDirectory.TempPath,
                TempDirectoryWritable = tempDirectory.Writable,
                TempDirectoryError = tempDirectory.Error,
                DotNetDiagnostics = dotnetDiagnostics,
                LinuxProcess = linuxProcess,
                LinuxContainer = linuxContainer,
                AdminTotp = adminTotp,
                DatabaseIntegrity = databaseIntegrity,
                CpuFeatures = cpuFeatures,
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

        private static IReadOnlyList<SecurityDiagnosticWarningDto> BuildWarnings(
            bool isContainer,
            bool isPublicInstance,
            MasterKeyRuntimeState masterKey,
            DotNetDiagnosticsDto dotnetDiagnostics,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer,
            AdminTotpDiagnosticsDto adminTotp,
            DatabaseIntegrityDiagnosticsDto databaseIntegrity,
            TempDirectoryProbeResult tempDirectory)
        {
            var warnings = new List<SecurityDiagnosticWarningDto>();
            AddPublicInstanceWarning(warnings, isPublicInstance);
            AddMasterKeyWarning(warnings, masterKey);
            AddAdminTotpWarning(warnings, adminTotp);
            AddDotNetDiagnosticsWarning(warnings, dotnetDiagnostics);
            AddTempDirectoryWarning(warnings, tempDirectory);
            AddLinuxProcessWarnings(warnings, isContainer, linuxProcess);
            AddLinuxContainerWarnings(warnings, isContainer, linuxProcess, linuxContainer);
            AddHardeningWarning(warnings, linuxProcess);
            AddDatabaseIntegrityWarnings(warnings, databaseIntegrity);
            return warnings;
        }

        private static void AddTempDirectoryWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            TempDirectoryProbeResult tempDirectory)
        {
            if (tempDirectory.Writable)
            {
                return;
            }

            string tempPath = string.IsNullOrWhiteSpace(tempDirectory.TempPath)
                ? "unknown path"
                : tempDirectory.TempPath;
            string error = string.IsNullOrWhiteSpace(tempDirectory.Error)
                ? string.Empty
                : $" Error: {tempDirectory.Error}";

            warnings.Add(new SecurityDiagnosticWarningDto
            {
                Code = "temp-directory-not-writable",
                Severity = "critical",
                Message = $"Cotton cannot write to the OS temp directory ({tempPath}). Database dumps/restores, S3 upload spooling, and preview tooling require writable scratch space. Mount a writable /tmp when using read_only: true, or bind-mount a fast writable disk at /tmp.{error}",
            });
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
                    Message = $"{databaseIntegrity.UnsignedProtectedRows} protected database rows are missing valid integrity signatures. Restore the affected rows from a trusted backup or run the required transition version before upgrading.",
                });
            }
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
                Message = "The container root filesystem is writable. Set read_only: true, keep /app/files as the persistent writable data volume, and mount writable scratch storage at /tmp.",
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
