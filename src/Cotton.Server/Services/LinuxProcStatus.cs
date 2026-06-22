// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Runtime.InteropServices;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Reports linux proc status.
    /// </summary>
    public record LinuxProcStatus(
        int? NoNewPrivileges,
        int? SeccompMode,
        int? SeccompFilters,
        string? EffectiveCapabilitiesHex,
        bool? HasSysPtraceCapability)
    {
        /// <summary>
        /// Creates an empty value object.
        /// </summary>
        public static LinuxProcStatus Empty { get; } = new(null, null, null, null, null);
    }
}
