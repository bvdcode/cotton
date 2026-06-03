// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopAutostartServiceFactory
{
    public static IAutostartService CreateDefault()
    {
        if (OperatingSystem.IsLinux())
        {
            return XdgAutostartService.CreateDefault();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsRunAutostartService(AutostartLaunchCommand.CreateDefault());
        }

        return new UnsupportedAutostartService();
    }
}
