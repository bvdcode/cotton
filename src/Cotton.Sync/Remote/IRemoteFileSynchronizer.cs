// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Sync.Local;

namespace Cotton.Sync.Remote;

/// <summary>
/// Performs remote file mutations and transfers for the synchronization engine.
/// </summary>
public interface IRemoteFileSynchronizer
{
    /// <summary>
    /// Uploads a local file as either a new remote entry or a content update for an existing entry.
    /// </summary>
    Task<NodeFileManifestDto> UploadFileAsync(
        Guid rootNodeId,
        string relativePath,
        LocalFileSnapshot localFile,
        NodeFileManifestDto? existingRemoteFile = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a remote file into the supplied destination stream.
    /// </summary>
    Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a remote file entry.
    /// </summary>
    Task DeleteFileAsync(
        Guid nodeFileId,
        bool skipTrash = false,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);
}
