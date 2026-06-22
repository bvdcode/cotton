// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
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
}
