// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges
{

    /// <summary>
    /// Describes a local filesystem change kind.
    /// </summary>
    public enum LocalSyncRootChangeKind
    {
        /// <summary>
        /// No local change kind was supplied.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A local item was created.
        /// </summary>
        Created = 1,

        /// <summary>
        /// A local item was modified.
        /// </summary>
        Changed = 2,

        /// <summary>
        /// A local item was deleted.
        /// </summary>
        Deleted = 3,

        /// <summary>
        /// A local item was renamed.
        /// </summary>
        Renamed = 4,

        /// <summary>
        /// The watcher encountered an error and the sync pair should be reconciled.
        /// </summary>
        Error = 5,
    }
}
