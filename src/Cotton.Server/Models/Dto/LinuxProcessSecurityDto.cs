// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Linux process security posture reported by the diagnostics API.
    /// </summary>
    public class LinuxProcessSecurityDto
    {
        /// <summary>
        /// Whether process hardening was requested by configuration.
        /// </summary>
        public bool HardeningRequested { get; init; }

        /// <summary>
        /// Whether process hardening was successfully applied at startup.
        /// </summary>
        public bool HardeningApplied { get; init; }

        /// <summary>
        /// Error reported while applying hardening, or null if it succeeded.
        /// </summary>
        public string? HardeningError { get; init; }

        /// <summary>
        /// Value of the process "dumpable" flag; 0 disables core dumps and debugger attach.
        /// </summary>
        public int? Dumpable { get; init; }

        /// <summary>
        /// Effective user id the process runs as.
        /// </summary>
        public uint? EffectiveUserId { get; init; }

        /// <summary>
        /// Whether the effective user id is root (0).
        /// </summary>
        public bool? RunningAsRoot { get; init; }

        /// <summary>
        /// Value of the no_new_privs flag; 1 blocks privilege escalation through exec.
        /// </summary>
        public int? NoNewPrivileges { get; init; }

        /// <summary>
        /// Seccomp mode from /proc/self/status (0 = disabled, 1 = strict, 2 = filter).
        /// </summary>
        public int? SeccompMode { get; init; }

        /// <summary>
        /// Number of installed seccomp filters.
        /// </summary>
        public int? SeccompFilters { get; init; }

        /// <summary>
        /// Effective Linux capability set, hex-encoded.
        /// </summary>
        public string? EffectiveCapabilitiesHex { get; init; }

        /// <summary>
        /// Whether the process holds the CAP_SYS_PTRACE capability (allows debugger attach).
        /// </summary>
        public bool? HasSysPtraceCapability { get; init; }
    }
}
