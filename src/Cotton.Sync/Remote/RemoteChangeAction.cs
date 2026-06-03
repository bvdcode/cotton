// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote;

/// <summary>
/// Describes the semantic action represented by a remote change-feed item.
/// </summary>
public enum RemoteChangeAction
{
    /// <summary>A remote item was created.</summary>
    Created = 0,

    /// <summary>A remote file content payload was updated.</summary>
    ContentUpdated = 1,

    /// <summary>A remote item was renamed.</summary>
    Renamed = 2,

    /// <summary>A remote item was moved to another parent.</summary>
    Moved = 3,

    /// <summary>A remote item was deleted or moved to trash.</summary>
    Deleted = 4,

    /// <summary>A remote item was restored from trash.</summary>
    Restored = 5,
}
