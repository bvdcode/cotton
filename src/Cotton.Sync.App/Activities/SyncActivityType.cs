// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Activities;

/// <summary>
/// Identifies a sync activity entry type.
/// </summary>
public enum SyncActivityType
{
    /// <summary>
    /// A local item was uploaded.
    /// </summary>
    Uploaded,

    /// <summary>
    /// A remote item was downloaded.
    /// </summary>
    Downloaded,

    /// <summary>
    /// A local item was deleted or moved to safe delete storage.
    /// </summary>
    DeletedLocal,

    /// <summary>
    /// A remote item was deleted or moved to trash.
    /// </summary>
    DeletedRemote,

    /// <summary>
    /// A conflict was created.
    /// </summary>
    Conflict,

    /// <summary>
    /// An item was skipped deliberately.
    /// </summary>
    Skipped,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error,

    /// <summary>
    /// A warning occurred.
    /// </summary>
    Warning,
}
