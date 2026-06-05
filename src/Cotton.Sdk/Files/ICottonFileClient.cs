// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Files;

namespace Cotton.Sdk.Files;

/// <summary>
/// Provides file operations used by synchronization clients.
/// </summary>
public interface ICottonFileClient
{
    /// <summary>
    /// Creates a file entry from already uploaded chunks.
    /// </summary>
    Task<NodeFileManifestDto> CreateFromChunksAsync(CreateFileFromChunksRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates file content from already uploaded chunks.
    /// </summary>
    Task<NodeFileManifestDto> UpdateContentAsync(
        Guid nodeFileId,
        CreateFileFromChunksRequestDto request,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a file to a different parent node.
    /// </summary>
    Task<NodeFileManifestDto> MoveAsync(
        Guid nodeFileId,
        Guid parentId,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a file.
    /// </summary>
    Task<NodeFileManifestDto> RenameAsync(
        Guid nodeFileId,
        string name,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges metadata into a file entry.
    /// </summary>
    Task<NodeFileManifestDto> UpdateMetadataAsync(Guid nodeFileId, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file entry.
    /// </summary>
    Task DeleteAsync(
        Guid nodeFileId,
        bool skipTrash = false,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a trashed file entry.
    /// </summary>
    Task<NodeFileManifestDto> RestoreAsync(Guid nodeFileId, RestoreItemRequestDto? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists file versions.
    /// </summary>
    Task<List<FileVersionDto>> GetVersionsAsync(Guid nodeFileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads owned file content through bearer-token authentication.
    /// </summary>
    Task DownloadContentAsync(Guid nodeFileId, Stream destination, bool download = false, IProgress<long>? progress = null, CancellationToken cancellationToken = default);
}
