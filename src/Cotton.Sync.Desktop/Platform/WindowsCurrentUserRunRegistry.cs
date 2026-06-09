// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Cotton.Sync.Desktop.Platform
{

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsCurrentUserRunRegistry : IWindowsRunRegistry
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public string? GetValue(string valueName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(valueName) as string;
        }

        public void SetValue(string valueName, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
            ArgumentNullException.ThrowIfNull(value);
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath, writable: true)
                ?? throw new InvalidOperationException("Unable to open the Windows startup registry key.");
            key.SetValue(valueName, value, RegistryValueKind.String);
        }

        public void DeleteValue(string valueName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
