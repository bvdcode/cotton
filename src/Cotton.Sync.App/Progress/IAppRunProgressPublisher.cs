// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress;

/// <summary>
/// Publishes aggregate sync-pass progress entries to interested subscribers.
/// </summary>
public interface IAppRunProgressPublisher : IObservable<AppRunProgress>
{
    /// <summary>
    /// Publishes one aggregate sync-pass progress sample.
    /// </summary>
    void Publish(AppRunProgress progress);
}
