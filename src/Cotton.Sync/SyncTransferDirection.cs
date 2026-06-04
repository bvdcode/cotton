// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Defines the direction of a file transfer.
/// </summary>
public enum SyncTransferDirection
{
    /// <summary>
    /// The transfer direction is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Local content is being uploaded to Cotton Cloud.
    /// </summary>
    Upload = 1,

    /// <summary>
    /// Remote content is being downloaded to the local sync folder.
    /// </summary>
    Download = 2,
}
