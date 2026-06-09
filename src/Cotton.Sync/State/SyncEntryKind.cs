// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State
{

    /// <summary>
    /// Defines the kind of remote/local entry tracked by the sync state store.
    /// </summary>
    public enum SyncEntryKind
    {
        /// <summary>
        /// No sync entry kind was supplied.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Represents a file entry.
        /// </summary>
        File = 1,

        /// <summary>
        /// Represents a folder entry.
        /// </summary>
        Directory = 2,
    }
}
