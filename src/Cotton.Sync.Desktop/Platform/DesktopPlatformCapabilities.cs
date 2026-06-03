// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopPlatformCapabilities
{
    public static bool IsAutostartSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    public static bool IsTrayLifecycleSupported => OperatingSystem.IsWindows();
}
