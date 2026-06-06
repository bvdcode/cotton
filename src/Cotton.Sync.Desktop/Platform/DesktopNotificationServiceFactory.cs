// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal static class DesktopNotificationServiceFactory
{
    private const string NotifySendCommandName = "notify-send";
    private const string WindowsPowerShellCommandName = "powershell.exe";
    private const string PowerShellCoreCommandName = "pwsh.exe";
    private const string LinuxNotificationDetails =
        "requires DBus session bus; sender name, icon rendering, timeout, and actions depend on the desktop notification daemon; actions are not used";
    private const string WindowsNotificationDetails =
        "installed sender identity depends on a registered Start Menu AppUserModelID shortcut; debug launches can show the raw process identity";

    public static IDesktopNotificationService CreateDefault()
    {
        return CreateForPlatform(
            ResolvePlatform(),
            Environment.GetEnvironmentVariable("PATH"),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
    }

    public static DesktopNotificationCapabilitySnapshot CreateCapabilitySnapshot()
    {
        return CreateCapabilitySnapshot(
            ResolvePlatform(),
            Environment.GetEnvironmentVariable("PATH"),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
    }

    internal static IDesktopNotificationService CreateForPlatform(
        DesktopNotificationPlatform platform,
        string? pathValue,
        string? appBaseDirectory = null,
        string? dbusSessionBusAddress = null)
    {
        DesktopNotificationCapabilitySnapshot capabilities = CreateCapabilitySnapshot(
            platform,
            pathValue,
            appBaseDirectory,
            dbusSessionBusAddress);
        if (capabilities.Platform == DesktopNotificationPlatform.Linux)
        {
            return !capabilities.IsSupported || capabilities.ExecutablePath is null
                ? new UnsupportedDesktopNotificationService()
                : new NotifySendNotificationService(capabilities.ExecutablePath, capabilities.IconPath);
        }

        if (capabilities.Platform == DesktopNotificationPlatform.Windows)
        {
            return capabilities.ExecutablePath is null
                ? new UnsupportedDesktopNotificationService()
                : new WindowsToastNotificationService(capabilities.ExecutablePath, capabilities.IconPath);
        }

        return new UnsupportedDesktopNotificationService();
    }

    internal static DesktopNotificationCapabilitySnapshot CreateCapabilitySnapshot(
        DesktopNotificationPlatform platform,
        string? pathValue,
        string? appBaseDirectory = null,
        string? dbusSessionBusAddress = null)
    {
        string? iconPath = ResolveNotificationIconPath(appBaseDirectory ?? AppContext.BaseDirectory);
        if (platform == DesktopNotificationPlatform.Linux)
        {
            string? notifySendPath = ResolveExecutablePath(
                NotifySendCommandName,
                pathValue);
            bool hasSessionBus = !string.IsNullOrWhiteSpace(dbusSessionBusAddress);
            return new DesktopNotificationCapabilitySnapshot(
                Platform: platform,
                AdapterName: NotifySendCommandName,
                IsSupported: notifySendPath is not null && hasSessionBus,
                AppName: DesktopNotificationIdentity.AppName,
                AppUserModelId: null,
                ExecutablePath: notifySendPath,
                IconPath: iconPath,
                PlatformDetails: "session bus: " + (hasSessionBus ? "available" : "missing") + "; " + LinuxNotificationDetails);
        }

        if (platform == DesktopNotificationPlatform.Windows)
        {
            string? powerShellPath = ResolveFirstExecutablePath(
                [WindowsPowerShellCommandName, PowerShellCoreCommandName],
                pathValue);
            return new DesktopNotificationCapabilitySnapshot(
                Platform: platform,
                AdapterName: "Windows toast",
                IsSupported: powerShellPath is not null,
                AppName: DesktopNotificationIdentity.AppName,
                AppUserModelId: DesktopAppIdentity.AppUserModelId,
                ExecutablePath: powerShellPath,
                IconPath: iconPath,
                PlatformDetails: WindowsNotificationDetails);
        }

        return new DesktopNotificationCapabilitySnapshot(
            Platform: DesktopNotificationPlatform.Unsupported,
            AdapterName: "Unsupported",
            IsSupported: false,
            AppName: DesktopNotificationIdentity.AppName,
            AppUserModelId: null,
            ExecutablePath: null,
            IconPath: iconPath);
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
