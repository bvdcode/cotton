// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote;

/// <summary>
/// Describes the remote entity kind targeted by a change-feed item.
/// </summary>
public enum RemoteChangeTargetKind
{
    /// <summary>The change targets a file entry.</summary>
    File = 0,

    /// <summary>The change targets a folder node.</summary>
    Folder = 1,
}
