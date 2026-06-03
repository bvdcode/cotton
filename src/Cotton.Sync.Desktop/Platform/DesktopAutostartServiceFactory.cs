// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopAutostartServiceFactory
{
    public static IAutostartService CreateDefault()
    {
        if (!DesktopPlatformCapabilities.IsAutostartSupported)
        {
            return new UnsupportedAutostartService();
        }

        if (OperatingSystem.IsLinux())
        {
            return XdgAutostartService.CreateDefault(DesktopPlatformCapabilities.IsTrayLifecycleSupported);
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsRunAutostartService(
                AutostartLaunchCommand.CreateDefault(DesktopPlatformCapabilities.IsTrayLifecycleSupported));
        }

        return new UnsupportedAutostartService();
    }
}
