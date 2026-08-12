// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class LinuxProcessSecurityDto
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
}
