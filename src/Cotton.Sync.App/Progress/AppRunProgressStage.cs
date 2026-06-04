// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress;

/// <summary>
/// Defines the current stage of one synchronization pass.
/// </summary>
public enum AppRunProgressStage
{
    /// <summary>
    /// The stage is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The local folder is being scanned.
    /// </summary>
    ScanningLocal = 1,

    /// <summary>
    /// The remote folder is being scanned.
    /// </summary>
    ScanningRemote = 2,

    /// <summary>
    /// Folder entries are being reconciled.
    /// </summary>
    ReconcilingDirectories = 3,

    /// <summary>
    /// File entries are being reconciled.
    /// </summary>
    ReconcilingFiles = 4,

    /// <summary>
    /// The synchronization pass completed.
    /// </summary>
    Completed = 5,
}
