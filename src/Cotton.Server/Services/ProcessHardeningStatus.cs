// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Runtime.InteropServices;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Reports process hardening status.
    /// </summary>
    public record ProcessHardeningStatus(
        bool Requested,
        bool Applied,
        string? Error,
        int? DumpableAfter);
}
