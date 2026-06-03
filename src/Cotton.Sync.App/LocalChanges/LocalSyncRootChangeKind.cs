// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges;

/// <summary>
/// Describes a local filesystem change kind.
/// </summary>
public enum LocalSyncRootChangeKind
{
    /// <summary>
    /// A local item was created.
    /// </summary>
    Created,

    /// <summary>
    /// A local item was modified.
    /// </summary>
    Changed,

    /// <summary>
    /// A local item was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// A local item was renamed.
    /// </summary>
    Renamed,

    /// <summary>
    /// The watcher encountered an error and the sync pair should be reconciled.
    /// </summary>
    Error,
}
