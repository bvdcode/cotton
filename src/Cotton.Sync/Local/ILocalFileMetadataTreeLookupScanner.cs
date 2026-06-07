// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Scans a local tree into path lookups without retaining an intermediate tree list.
/// </summary>
public interface ILocalFileMetadataTreeLookupScanner : ILocalFileMetadataTreeScanner
{
    /// <summary>
    /// Scans a local tree and returns metadata-only file and directory lookups.
    /// </summary>
    Task<LocalTreeLookupSnapshot> ScanTreeMetadataLookupsAsync(
        string rootPath,
        IProgress<LocalTreeScanProgress>? progress,
        CancellationToken cancellationToken = default);
}
