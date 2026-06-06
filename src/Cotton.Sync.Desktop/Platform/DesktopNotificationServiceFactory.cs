// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopNotificationServiceFactory
{
    private const string NotifySendCommandName = "notify-send";
    private const string WindowsPowerShellCommandName = "powershell.exe";
    private const string PowerShellCoreCommandName = "pwsh.exe";

    public static IDesktopNotificationService CreateDefault()
    {
        return CreateForPlatform(
            ResolvePlatform(),
            Environment.GetEnvironmentVariable("PATH"),
            AppContext.BaseDirectory);
    }

    internal static IDesktopNotificationService CreateForPlatform(
        DesktopNotificationPlatform platform,
        string? pathValue,
        string? appBaseDirectory = null)
    {
        string? iconPath = ResolveNotificationIconPath(appBaseDirectory ?? AppContext.BaseDirectory);
        if (platform == DesktopNotificationPlatform.Linux)
        {
            string? notifySendPath = ResolveExecutablePath(
                NotifySendCommandName,
                pathValue);
            return notifySendPath is null
                ? new UnsupportedDesktopNotificationService()
                : new NotifySendNotificationService(notifySendPath, iconPath);
        }

        if (platform == DesktopNotificationPlatform.Windows)
        {
            string? powerShellPath = ResolveFirstExecutablePath(
                [WindowsPowerShellCommandName, PowerShellCoreCommandName],
                pathValue);
            return powerShellPath is null
                ? new UnsupportedDesktopNotificationService()
                : new WindowsToastNotificationService(powerShellPath, iconPath);
        }

        return new UnsupportedDesktopNotificationService();
    }

    internal static string? ResolveExecutablePath(string commandName, string? pathValue)
    {
        return ExecutablePathResolver.Resolve(commandName, pathValue);
    }

    internal static string? ResolveNotificationIconPath(string appBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
        string candidate = Path.Combine(appBaseDirectory, "Assets", "icon-192.png");
        return File.Exists(candidate) ? candidate : null;
    }

    private static DesktopNotificationPlatform ResolvePlatform()
    {
        if (OperatingSystem.IsLinux())
        {
            return DesktopNotificationPlatform.Linux;
        }

        if (OperatingSystem.IsWindows())
        {
            return DesktopNotificationPlatform.Windows;
        }

        return DesktopNotificationPlatform.Unsupported;
    }

    private static string? ResolveFirstExecutablePath(
        IReadOnlyList<string> commandNames,
        string? pathValue)
    {
        foreach (string commandName in commandNames)
        {
            string? executablePath = ResolveExecutablePath(commandName, pathValue);
            if (executablePath is not null)
            {
                return executablePath;
            }
        }

        return null;
    }
}
