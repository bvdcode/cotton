// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services
{
    internal static class LinuxSecurityWarningBuilder
    {
        public static void AddWarnings(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            bool isContainer,
            LinuxProcessSecurityDto linuxProcess,
            LinuxContainerSecurityDto linuxContainer)
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
            if (isContainer)
            {
                AddRootFilesystemWarning(warnings, linuxContainer);
                AddDockerSocketWarning(warnings, linuxContainer);
                AddHostPidNamespaceWarning(warnings, linuxContainer);
                AddMandatoryAccessControlWarning(warnings, linuxContainer);
            }

            AddCoreDumpLimitWarning(warnings, linuxProcess, linuxContainer);
        }

        private static void AddDumpableWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.Dumpable == 0)
            {
                return;
            }

            warnings.Add(Create(
                "process-dumpable",
                "warning",
                "The Linux process is dumpable. Set COTTON_PROCESS_HARDENING=true or run the official container defaults to request PR_SET_DUMPABLE=0 early at startup."));
        }

        private static void AddPtraceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.HasSysPtraceCapability != true)
            {
                return;
            }

            warnings.Add(Create(
                "sys-ptrace-capability",
                "critical",
                "CAP_SYS_PTRACE is effective for this process. Avoid SYS_PTRACE/privileged containers unless actively debugging."));
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

            warnings.Add(Create(
                "new-privileges-allowed",
                isContainer ? "warning" : "info",
                "no-new-privileges is not enabled. In Docker Compose, security_opt: [\"no-new-privileges:true\"] is a cheap hardening layer."));
        }

        private static void AddSeccompWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.SeccompMode != 0)
            {
                return;
            }

            warnings.Add(Create(
                "seccomp-disabled",
                "warning",
                "Seccomp appears disabled. Docker's default seccomp profile is a useful baseline; avoid seccomp=unconfined in production."));
        }

        private static void AddRootWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxProcessSecurityDto linuxProcess)
        {
            if (linuxProcess.RunningAsRoot != true)
            {
                return;
            }

            warnings.Add(Create(
                "running-as-root",
                "info",
                "The process is running as root. This may be acceptable for simple self-hosting, but a dedicated non-root UID is stronger when volume permissions are prepared for it."));
        }

        private static void AddRootFilesystemWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (linuxContainer.RootFilesystemReadOnly != false)
            {
                return;
            }

            warnings.Add(Create(
                "root-filesystem-writable",
                "info",
                "The container root filesystem is writable. Set read_only: true, keep /app/files as the persistent writable data volume, and mount writable scratch storage at /tmp."));
        }

        private static void AddDockerSocketWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (!linuxContainer.DockerSocketMounted)
            {
                return;
            }

            warnings.Add(Create(
                "docker-socket-mounted",
                "critical",
                "The Docker socket is visible inside the Cotton container. Remove the socket mount; it is effectively host-root access from the web process."));
        }

        private static void AddHostPidNamespaceWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            if (linuxContainer.HostPidNamespaceLikely != true)
            {
                return;
            }

            warnings.Add(Create(
                "host-pid-namespace",
                "critical",
                "Cotton appears to share the host PID namespace. Remove pid: host so process isolation and procfs visibility stay inside the container boundary."));
        }

        private static void AddMandatoryAccessControlWarning(
            ICollection<SecurityDiagnosticWarningDto> warnings,
            LinuxContainerSecurityDto linuxContainer)
        {
            bool appArmorUnconfined = linuxContainer.AppArmorProfile?.StartsWith(
                "unconfined",
                StringComparison.OrdinalIgnoreCase) == true;
            bool hasMacProfile = !string.IsNullOrWhiteSpace(linuxContainer.AppArmorProfile)
                || !string.IsNullOrWhiteSpace(linuxContainer.SelinuxContext);
            bool selinuxPermissive = linuxContainer.SelinuxEnforcing == false;
            if (hasMacProfile && !appArmorUnconfined && !selinuxPermissive)
            {
                return;
            }

            warnings.Add(Create(
                "mandatory-access-control-unconfined",
                "warning",
                "No enforcing AppArmor or SELinux confinement was detected for the container. Use Docker default AppArmor, a custom AppArmor profile, or an enforcing SELinux container context."));
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

            warnings.Add(Create(
                "core-dumps-enabled",
                "warning",
                "Core dump limits allow dumps while the process may be dumpable. Set ulimit core=0 and keep COTTON_PROCESS_HARDENING=true so crashes cannot write memory snapshots containing secrets."));
        }

        private static SecurityDiagnosticWarningDto Create(string code, string severity, string message)
        {
            return new SecurityDiagnosticWarningDto
            {
                Code = code,
                Severity = severity,
                Message = message,
            };
        }
    }
}
