// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform
{
    internal interface IAutostartService
    {
        bool IsSupported { get; }

        Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

        Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    }
}
