// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Status
{

    /// <summary>
    /// Publishes application status snapshots to UI and command surfaces.
    /// </summary>
    public interface IAppStatusPublisher : IObservable<SyncAppStatus>
    {
        /// <summary>
        /// Gets the latest published status.
        /// </summary>
        SyncAppStatus Current { get; }

        /// <summary>
        /// Publishes a new status snapshot.
        /// </summary>
        void Publish(SyncAppStatus status);
    }
}
