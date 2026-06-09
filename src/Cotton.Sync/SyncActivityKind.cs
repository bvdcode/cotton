// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{

    /// <summary>
    /// Defines synchronization activity categories emitted during a sync pass.
    /// </summary>
    public enum SyncActivityKind
    {
        /// <summary>
        /// No activity kind was supplied.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A local file was uploaded to the remote folder.
        /// </summary>
        Uploaded = 1,

        /// <summary>
        /// A remote file was downloaded to the local folder.
        /// </summary>
        Downloaded = 2,

        /// <summary>
        /// A local file was deleted because the baseline-known remote file was deleted.
        /// </summary>
        DeletedLocal = 3,

        /// <summary>
        /// A remote file was deleted because the baseline-known local file was deleted.
        /// </summary>
        DeletedRemote = 4,

        /// <summary>
        /// Divergent local and remote changes were preserved without overwriting either side silently.
        /// </summary>
        Conflict = 5,

        /// <summary>
        /// A requested action was deliberately skipped by a safety guard.
        /// </summary>
        Skipped = 6,
    }
}
