// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Local;

/// <summary>
/// Describes progress while scanning a local folder tree.
/// </summary>
public sealed class LocalTreeScanProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTreeScanProgress" /> class.
    /// </summary>
    public LocalTreeScanProgress(int filesScanned, int directoriesScanned, string? currentPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(filesScanned);
        ArgumentOutOfRangeException.ThrowIfNegative(directoriesScanned);
        FilesScanned = filesScanned;
        DirectoriesScanned = directoriesScanned;
        CurrentPath = string.IsNullOrWhiteSpace(currentPath) ? string.Empty : SyncPath.Normalize(currentPath);
    }

    /// <summary>
    /// Gets the number of file entries discovered so far.
    /// </summary>
    public int FilesScanned { get; }

    /// <summary>
    /// Gets the number of directory entries discovered so far.
    /// </summary>
    public int DirectoriesScanned { get; }

    /// <summary>
    /// Gets the most recent discovered path when available.
    /// </summary>
    public string CurrentPath { get; }
}
