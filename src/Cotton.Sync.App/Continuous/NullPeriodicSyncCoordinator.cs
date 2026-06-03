// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Continuous;

/// <summary>
/// No-op periodic sync coordinator used when periodic safety sync is not configured.
/// </summary>
public sealed class NullPeriodicSyncCoordinator : IPeriodicSyncCoordinator
{
    /// <summary>
    /// Gets the shared no-op coordinator instance.
    /// </summary>
    public static NullPeriodicSyncCoordinator Instance { get; } = new();

    private NullPeriodicSyncCoordinator()
    {
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
