// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{
    internal sealed class UnsupportedAutostartService : IAutostartService
    {
        public bool IsSupported => false;

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (enabled)
            {
                throw new NotSupportedException("Autostart is not supported on this platform yet.");
            }

            return Task.CompletedTask;
        }
    }
}
