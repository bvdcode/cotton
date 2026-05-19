// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services
{
    public sealed class SecurityDiagnosticsService(
        ProcessHardeningStatus hardeningStatus,
        MasterKeyRuntimeState masterKeyRuntimeState)
    {
        public SecurityDiagnosticsDto GetSnapshot()
        {
            LinuxProcStatus procStatus = LinuxProcessHardening.SnapshotProcStatus();
            uint? effectiveUserId = LinuxProcessHardening.TryGetEffectiveUserId();
            int? dumpable = LinuxProcessHardening.TryGetDumpable() ?? hardeningStatus.DumpableAfter;
            string? dotnetEnableDiagnostics = Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics");
            string? comPlusEnableDiagnostics = Environment.GetEnvironmentVariable("COMPlus_EnableDiagnostics");
            bool dotnetDiagnosticsDisabled = IsZero(dotnetEnableDiagnostics) || IsZero(comPlusEnableDiagnostics);
            bool isContainer = IsContainer();

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

            return new SecurityDiagnosticsDto
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                IsLinux = OperatingSystem.IsLinux(),
                IsContainer = isContainer,
                MasterKeySource = masterKeyRuntimeState.Source,
                MasterKeyEnvironmentVariableWasConfigured = masterKeyRuntimeState.EnvironmentVariableWasConfigured,
                MasterKeyEnvironmentVariablePresentInProcess = masterKeyRuntimeState.EnvironmentVariablePresentAfterResolution,
                DotNetDiagnostics = dotnetDiagnostics,
                LinuxProcess = linuxProcess,
                Warnings = BuildWarnings(isContainer, masterKeyRuntimeState, dotnetDiagnostics, linuxProcess),
            };
        }

        private static IReadOnlyList<SecurityDiagnosticWarningDto> BuildWarnings(
            bool isContainer,
            MasterKeyRuntimeState masterKey,
            DotNetDiagnosticsDto dotnetDiagnostics,
            LinuxProcessSecurityDto linuxProcess)
        {
            var warnings = new List<SecurityDiagnosticWarningDto>();

            if (masterKey.EnvironmentVariableWasConfigured)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "master-key-from-environment",
                    Severity = "info",
                    Message = "This process was unlocked from COTTON_MASTER_KEY. Cotton clears its own process environment after reading it, but container runtimes may still expose configured environment variables through deployment metadata or docker exec environments.",
                });
            }

            if (!dotnetDiagnostics.Disabled)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "dotnet-diagnostics-enabled",
                    Severity = "warning",
                    Message = "DOTNET diagnostics appear enabled. Production containers should set DOTNET_EnableDiagnostics=0 to disable debugger, profiler, EventPipe, and dump collection endpoints.",
                });
            }

            if (OperatingSystem.IsLinux())
            {
                if (linuxProcess.Dumpable != 0)
                {
                    warnings.Add(new SecurityDiagnosticWarningDto
                    {
                        Code = "process-dumpable",
                        Severity = "warning",
                        Message = "The Linux process is dumpable. Set COTTON_PROCESS_HARDENING=true or run the official container defaults to request PR_SET_DUMPABLE=0 early at startup.",
                    });
                }

                if (linuxProcess.HasSysPtraceCapability == true)
                {
                    warnings.Add(new SecurityDiagnosticWarningDto
                    {
                        Code = "sys-ptrace-capability",
                        Severity = "critical",
                        Message = "CAP_SYS_PTRACE is effective for this process. Avoid SYS_PTRACE/privileged containers unless actively debugging.",
                    });
                }

                if (linuxProcess.NoNewPrivileges == 0)
                {
                    warnings.Add(new SecurityDiagnosticWarningDto
                    {
                        Code = "new-privileges-allowed",
                        Severity = isContainer ? "warning" : "info",
                        Message = "no-new-privileges is not enabled. In Docker Compose, security_opt: [\"no-new-privileges:true\"] is a cheap hardening layer.",
                    });
                }

                if (linuxProcess.SeccompMode == 0)
                {
                    warnings.Add(new SecurityDiagnosticWarningDto
                    {
                        Code = "seccomp-disabled",
                        Severity = "warning",
                        Message = "Seccomp appears disabled. Docker's default seccomp profile is a useful baseline; avoid seccomp=unconfined in production.",
                    });
                }

                if (linuxProcess.RunningAsRoot == true)
                {
                    warnings.Add(new SecurityDiagnosticWarningDto
                    {
                        Code = "running-as-root",
                        Severity = "info",
                        Message = "The process is running as root. This may be acceptable for simple self-hosting, but a dedicated non-root UID is stronger when volume permissions are prepared for it.",
                    });
                }
            }

            if (linuxProcess.HardeningRequested && !linuxProcess.HardeningApplied)
            {
                warnings.Add(new SecurityDiagnosticWarningDto
                {
                    Code = "process-hardening-failed",
                    Severity = "warning",
                    Message = linuxProcess.HardeningError ?? "Process hardening was requested but did not apply.",
                });
            }

            return warnings;
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
