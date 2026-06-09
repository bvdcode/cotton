// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Runtime.Versioning;

namespace Cotton.Sync.Desktop.Platform
{

    internal sealed class WindowsRunAutostartService : IAutostartService
    {
        private const string ValueName = "Cotton Sync";

        private readonly AutostartLaunchCommand _launchCommand;
        private readonly IWindowsRunRegistry _registry;

        [SupportedOSPlatform("windows")]
        public WindowsRunAutostartService(AutostartLaunchCommand launchCommand)
            : this(launchCommand, new WindowsCurrentUserRunRegistry())
        {
        }

        internal WindowsRunAutostartService(AutostartLaunchCommand launchCommand, IWindowsRunRegistry registry)
        {
            _launchCommand = launchCommand ?? throw new ArgumentNullException(nameof(launchCommand));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public bool IsSupported => true;

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(string.Equals(
                _registry.GetValue(ValueName),
                _launchCommand.ToWindowsRunCommandLine(),
                StringComparison.Ordinal));
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (enabled)
            {
                _registry.SetValue(ValueName, _launchCommand.ToWindowsRunCommandLine());
            }
            else
            {
                _registry.DeleteValue(ValueName);
            }

            return Task.CompletedTask;
        }
    }
}
