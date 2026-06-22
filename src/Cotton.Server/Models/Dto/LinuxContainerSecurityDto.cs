// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
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
}
