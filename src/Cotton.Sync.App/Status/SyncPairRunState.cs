// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Status;

/// <summary>
/// Represents the current runtime state of one sync pair.
/// </summary>
public enum SyncPairRunState
{
    /// <summary>
    /// The sync pair is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// The sync pair is enabled and waiting for work.
    /// </summary>
    Idle,

    /// <summary>
    /// The sync pair is scanning local or remote state.
    /// </summary>
    Scanning,

    /// <summary>
    /// The sync pair is applying changes.
    /// </summary>
    Syncing,

    /// <summary>
    /// The sync pair is paused by the user.
    /// </summary>
    Paused,

    /// <summary>
    /// The sync pair cannot currently reach the server.
    /// </summary>
    Offline,

    /// <summary>
    /// The sync pair has conflicts that need attention.
    /// </summary>
    Conflict,

    /// <summary>
    /// The sync pair has an action-required error.
    /// </summary>
    Error,
}
