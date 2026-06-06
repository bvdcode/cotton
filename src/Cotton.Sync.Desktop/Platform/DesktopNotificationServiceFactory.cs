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

    public static DesktopNotificationCapabilitySnapshot CreateCapabilitySnapshot()
    {
        return CreateCapabilitySnapshot(
            ResolvePlatform(),
            Environment.GetEnvironmentVariable("PATH"),
            AppContext.BaseDirectory);
    }

    internal static IDesktopNotificationService CreateForPlatform(
        DesktopNotificationPlatform platform,
        string? pathValue,
        string? appBaseDirectory = null)
    {
        DesktopNotificationCapabilitySnapshot capabilities = CreateCapabilitySnapshot(
            platform,
            pathValue,
            appBaseDirectory);
        if (capabilities.Platform == DesktopNotificationPlatform.Linux)
        {
            return capabilities.ExecutablePath is null
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
        string? appBaseDirectory = null)
    {
        string? iconPath = ResolveNotificationIconPath(appBaseDirectory ?? AppContext.BaseDirectory);
        if (platform == DesktopNotificationPlatform.Linux)
        {
            string? notifySendPath = ResolveExecutablePath(
                NotifySendCommandName,
                pathValue);
            return new DesktopNotificationCapabilitySnapshot(
                Platform: platform,
                AdapterName: NotifySendCommandName,
                IsSupported: notifySendPath is not null,
                AppName: DesktopNotificationIdentity.AppName,
                AppUserModelId: null,
                ExecutablePath: notifySendPath,
                IconPath: iconPath);
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
                IconPath: iconPath);
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
