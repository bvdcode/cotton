// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Writes and deletes local files for the synchronization engine.
/// </summary>
public interface ILocalFileSyncWriter
{
    /// <summary>
    /// Writes a file through a temporary path and atomically replaces the target.
    /// </summary>
    Task WriteFileAsync(
        string rootPath,
        string relativePath,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        DateTime? lastWriteUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a local file if it exists.
    /// </summary>
    Task DeleteFileAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a unique conflict-copy relative path for a file.
    /// </summary>
    string CreateConflictRelativePath(string rootPath, string relativePath, DateTime timestampUtc);
}
