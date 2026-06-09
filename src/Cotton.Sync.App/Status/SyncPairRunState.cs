// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Status
{

    /// <summary>
    /// Represents the current runtime state of one sync pair.
    /// </summary>
    public enum SyncPairRunState
    {
        /// <summary>
        /// No runtime state was supplied.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The sync pair is disabled.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// The sync pair is enabled and waiting for work.
        /// </summary>
        Idle = 2,

        /// <summary>
        /// The sync pair is scanning local or remote state.
        /// </summary>
        Scanning = 3,

        /// <summary>
        /// The sync pair is applying changes.
        /// </summary>
        Syncing = 4,

        /// <summary>
        /// The sync pair is paused by the user.
        /// </summary>
        Paused = 5,

        /// <summary>
        /// The sync pair cannot currently reach the server.
        /// </summary>
        Offline = 6,

        /// <summary>
        /// The sync pair has conflicts that need attention.
        /// </summary>
        Conflict = 7,

        /// <summary>
        /// The sync pair has an action-required error.
        /// </summary>
        Error = 8,
    }
}
