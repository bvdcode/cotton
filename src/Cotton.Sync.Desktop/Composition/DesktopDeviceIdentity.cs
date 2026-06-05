// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Reflection;
using System.Runtime.InteropServices;
using Cotton.Contracts;

namespace Cotton.Sync.Desktop.Composition;

internal static class DesktopDeviceIdentity
{
    public static string CreateUserAgent()
    {
        return "CottonSyncDesktop/" + CreateVersionLabel() + " (" + CreatePlatformLabel() + ")";
    }

    public static string CreateDeviceName()
    {
        string machineName = Environment.MachineName.Trim();
        string deviceName = string.IsNullOrWhiteSpace(machineName)
            ? "Cotton Sync Desktop"
            : "Cotton Sync Desktop (" + machineName + ")";
        return deviceName.Length <= CottonClientHeaders.DeviceNameMaxLength
            ? deviceName
            : deviceName[..CottonClientHeaders.DeviceNameMaxLength];
    }

    private static string CreateVersionLabel()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "0.0.0"
            : version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
    }

    private static string CreatePlatformLabel()
    {
        string os = OperatingSystem.IsWindows()
            ? "Windows"
            : OperatingSystem.IsLinux()
                ? "Linux"
                : OperatingSystem.IsMacOS()
                    ? "macOS"
                    : "Unknown OS";
        return os + "; " + RuntimeInformation.ProcessArchitecture;
    }
}
