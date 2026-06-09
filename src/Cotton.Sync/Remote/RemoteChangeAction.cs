// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Describes the semantic action represented by a remote change-feed item.
    /// </summary>
    public enum RemoteChangeAction
    {
        /// <summary>No remote change action was supplied.</summary>
        Unknown = 0,

        /// <summary>A remote item was created.</summary>
        Created = 1,

        /// <summary>A remote file content payload was updated.</summary>
        ContentUpdated = 2,

        /// <summary>A remote item was renamed.</summary>
        Renamed = 3,

        /// <summary>A remote item was moved to another parent.</summary>
        Moved = 4,

        /// <summary>A remote item was deleted or moved to trash.</summary>
        Deleted = 5,

        /// <summary>A remote item was restored from trash.</summary>
        Restored = 6,
    }
}
