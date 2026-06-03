// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Cotton.Sync.Desktop.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsRunAutostartService : IAutostartService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Cotton Sync";

    private readonly AutostartLaunchCommand _launchCommand;

    public WindowsRunAutostartService(AutostartLaunchCommand launchCommand)
    {
        _launchCommand = launchCommand ?? throw new ArgumentNullException(nameof(launchCommand));
    }

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
        return Task.FromResult(string.Equals(
            key?.GetValue(ValueName) as string,
            _launchCommand.ToString(),
            StringComparison.Ordinal));
    }

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open the Windows startup registry key.");
        if (enabled)
        {
            key.SetValue(ValueName, _launchCommand.ToString(), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }
}
