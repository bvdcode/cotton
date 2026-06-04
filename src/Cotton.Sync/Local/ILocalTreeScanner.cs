// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Scans local files and directories for synchronization.
/// </summary>
public interface ILocalTreeScanner
{
    /// <summary>
    /// Scans a local root folder and returns stable directory and file snapshots.
    /// </summary>
    Task<LocalTreeSnapshot> ScanTreeAsync(string rootPath, CancellationToken cancellationToken = default);
}
