// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{

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
                XdgAutostartService? autostartService = XdgAutostartService.TryCreateDefault(
                    DesktopPlatformCapabilities.IsTrayLifecycleSupported);
                return autostartService is null ? new UnsupportedAutostartService() : autostartService;
            }

            if (OperatingSystem.IsWindows())
            {
                AutostartLaunchCommand? launchCommand = AutostartLaunchCommand.TryCreateDefault(
                    DesktopPlatformCapabilities.IsTrayLifecycleSupported);
                return launchCommand is null
                    ? new UnsupportedAutostartService()
                    : new WindowsRunAutostartService(launchCommand);
            }

            return new UnsupportedAutostartService();
        }
    }
}
