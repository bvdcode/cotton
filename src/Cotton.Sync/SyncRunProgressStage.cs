// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{

    /// <summary>
    /// Defines the current stage of one synchronization pass.
    /// </summary>
    public enum SyncRunProgressStage
    {
        /// <summary>
        /// The stage is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The sync engine is scanning the local folder.
        /// </summary>
        ScanningLocal = 1,

        /// <summary>
        /// The sync engine is crawling the remote folder.
        /// </summary>
        ScanningRemote = 2,

        /// <summary>
        /// The sync engine is reconciling folder entries.
        /// </summary>
        ReconcilingDirectories = 3,

        /// <summary>
        /// The sync engine is reconciling file entries.
        /// </summary>
        ReconcilingFiles = 4,

        /// <summary>
        /// The synchronization pass completed.
        /// </summary>
        Completed = 5,
    }
}
