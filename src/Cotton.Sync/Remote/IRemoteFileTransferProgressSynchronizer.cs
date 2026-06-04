// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Sync.Local;

namespace Cotton.Sync.Remote;

/// <summary>
/// Performs remote file transfers with byte-level progress reporting.
/// </summary>
public interface IRemoteFileTransferProgressSynchronizer : IRemoteFileSynchronizer
{
    /// <summary>
    /// Uploads a local file and reports byte-level upload progress.
    /// </summary>
    Task<NodeFileManifestDto> UploadFileAsync(
        Guid rootNodeId,
        string relativePath,
        LocalFileSnapshot localFile,
        NodeFileManifestDto? existingRemoteFile,
        IProgress<SyncTransferProgress>? transferProgress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a remote file and reports byte-level download progress.
    /// </summary>
    Task DownloadFileAsync(
        Guid nodeFileId,
        string relativePath,
        long? totalBytes,
        Stream destination,
        IProgress<SyncTransferProgress>? transferProgress,
        CancellationToken cancellationToken = default);
}
