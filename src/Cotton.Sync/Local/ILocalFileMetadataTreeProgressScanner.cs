// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Scans local directories and file metadata while reporting scan progress.
/// </summary>
public interface ILocalFileMetadataTreeProgressScanner : ILocalFileMetadataTreeScanner
{
    /// <summary>
    /// Scans a local root folder and reports metadata scan progress as files are discovered.
    /// </summary>
    Task<LocalTreeSnapshot> ScanTreeMetadataAsync(
        string rootPath,
        IProgress<LocalTreeScanProgress>? progress,
        CancellationToken cancellationToken = default);
}
