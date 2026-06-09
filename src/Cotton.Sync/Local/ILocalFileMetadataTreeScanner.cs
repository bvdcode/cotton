// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Scans local directories and file metadata without eagerly hashing file contents.
    /// </summary>
    public interface ILocalFileMetadataTreeScanner
    {
        /// <summary>
        /// Scans a local root folder and returns stable directory and file metadata snapshots.
        /// </summary>
        Task<LocalTreeSnapshot> ScanTreeMetadataAsync(string rootPath, CancellationToken cancellationToken = default);
    }
}
