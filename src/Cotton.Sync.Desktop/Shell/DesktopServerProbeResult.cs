// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal sealed record DesktopServerProbeResult(
        Uri ServerUrl,
        bool IsCottonServer,
        string? Product,
        string? InstanceIdHash);
}
