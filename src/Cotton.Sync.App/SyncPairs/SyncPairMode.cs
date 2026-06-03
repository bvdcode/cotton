// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs;

/// <summary>
/// Defines how a local folder is synchronized with a remote Cotton folder.
/// </summary>
public enum SyncPairMode
{
    /// <summary>
    /// Keeps a complete local mirror of the configured remote folder.
    /// </summary>
    FullMirror = 0,

    /// <summary>
    /// Reserves a future virtual-files mode backed by platform-specific placeholder APIs.
    /// </summary>
    VirtualFilesPlaceholder = 1,
}
