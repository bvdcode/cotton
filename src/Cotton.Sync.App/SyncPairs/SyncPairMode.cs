// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{
    /// <summary>
    /// Defines how a local folder is synchronized with a remote Cotton folder.
    /// </summary>
    public enum SyncPairMode
    {
        /// <summary>
        /// No sync mode was supplied.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Keeps a complete local mirror of the configured remote folder.
        /// </summary>
        FullMirror = 1,

        /// <summary>
        /// Reserves a future virtual-files mode backed by platform-specific placeholder APIs.
        /// </summary>
        VirtualFilesPlaceholder = 2,
    }
}
