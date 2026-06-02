// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Scans local files for synchronization.
/// </summary>
public interface ILocalFileScanner
{
    /// <summary>
    /// Scans a local root folder and returns stable file snapshots.
    /// </summary>
    Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a local root folder, reusing known hashes for unchanged files when possible.
    /// </summary>
    Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(
        string rootPath,
        IReadOnlyDictionary<string, LocalFileScanHint> hashHints,
        CancellationToken cancellationToken = default)
    {
        return ScanAsync(rootPath, cancellationToken);
    }
}
