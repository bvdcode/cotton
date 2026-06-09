// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{
    internal sealed record DesktopPlatformCapabilitySnapshot(
        string OperatingSystemName,
        string DesktopSession,
        string CurrentDesktop,
        bool IsAutostartSupported,
        bool IsTrayLifecycleSupported,
        string TrayLifecycleDetails);
}
