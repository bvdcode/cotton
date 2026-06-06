// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Activities;

/// <summary>
/// Publishes live synchronization activity entries to UI and command surfaces.
/// </summary>
public interface IAppActivityPublisher : IObservable<AppSyncActivity>
{
    /// <summary>
    /// Publishes one activity entry.
    /// </summary>
    void Publish(AppSyncActivity activity);
}
