// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Runtime.InteropServices;

namespace Cotton.Server.Services
{
    public record LinuxProcStatus(
        int? NoNewPrivileges,
        int? SeccompMode,
        int? SeccompFilters,
        string? EffectiveCapabilitiesHex,
        bool? HasSysPtraceCapability)
    {
        public static LinuxProcStatus Empty { get; } = new(null, null, null, null, null);
    }
}
