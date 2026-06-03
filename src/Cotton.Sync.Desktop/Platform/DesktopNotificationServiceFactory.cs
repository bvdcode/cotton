// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopNotificationServiceFactory
{
    private const string NotifySendCommandName = "notify-send";

    public static IDesktopNotificationService CreateDefault()
    {
        if (OperatingSystem.IsLinux())
        {
            string? notifySendPath = ResolveExecutablePath(
                NotifySendCommandName,
                Environment.GetEnvironmentVariable("PATH"));
            return notifySendPath is null
                ? new UnsupportedDesktopNotificationService()
                : new NotifySendNotificationService(notifySendPath);
        }

        return new UnsupportedDesktopNotificationService();
    }

    internal static string? ResolveExecutablePath(string commandName, string? pathValue)
    {
        return ExecutablePathResolver.Resolve(commandName, pathValue);
    }
}
