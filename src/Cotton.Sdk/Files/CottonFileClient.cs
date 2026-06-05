// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Files;

/// <summary>
/// Provides Cotton file operations used by synchronization clients.
/// </summary>
public sealed class CottonFileClient : ICottonFileClient
{
    private const string IfMatchHeaderName = "If-Match";

    private readonly CottonHttpTransport _transport;

    internal CottonFileClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Creates a file entry from already uploaded chunks.
    /// </summary>
    public Task<NodeFileManifestDto> CreateFromChunksAsync(
        CreateFileFromChunksRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Post,
            "/api/v1/files/from-chunks",
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates file content from already uploaded chunks.
    /// </summary>
    public Task<NodeFileManifestDto> UpdateContentAsync(
        Guid nodeFileId,
        CreateFileFromChunksRequestDto request,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"/api/v1/files/{nodeFileId}/update-content",
            request,
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves a file to a different parent node.
    /// </summary>
    public Task<NodeFileManifestDto> MoveAsync(
        Guid nodeFileId,
        Guid parentId,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"/api/v1/files/{nodeFileId}/move",
            new MoveFileRequestDto { ParentId = parentId },
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Renames a file.
    /// </summary>
    public Task<NodeFileManifestDto> RenameAsync(
        Guid nodeFileId,
        string name,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"/api/v1/files/{nodeFileId}/rename",
            new RenameFileRequestDto { Name = name.Trim() },
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Merges metadata into a file entry.
    /// </summary>
    public Task<NodeFileManifestDto> UpdateMetadataAsync(
        Guid nodeFileId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Patch,
            $"/api/v1/files/{nodeFileId}/metadata",
            new Dictionary<string, string>(metadata),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes a file entry.
    /// </summary>
    public Task DeleteAsync(
        Guid nodeFileId,
        bool skipTrash = false,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendNoContentAsync(
            HttpMethod.Delete,
            $"/api/v1/files/{nodeFileId}?skipTrash={skipTrash.ToString().ToLowerInvariant()}",
            headers: CreateIfMatchHeader(expectedETag),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Restores a trashed file entry.
    /// </summary>
    public Task<NodeFileManifestDto> RestoreAsync(
        Guid nodeFileId,
        RestoreItemRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeFileManifestDto>(
            HttpMethod.Post,
            $"/api/v1/files/{nodeFileId}/restore",
            request ?? new RestoreItemRequestDto(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists file versions.
    /// </summary>
    public Task<List<FileVersionDto>> GetVersionsAsync(Guid nodeFileId, CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<List<FileVersionDto>>(
            HttpMethod.Get,
            $"/api/v1/files/{nodeFileId}/versions",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Downloads owned file content through bearer-token authentication.
    /// </summary>
    public Task DownloadContentAsync(
        Guid nodeFileId,
        Stream destination,
        bool download = false,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string path = $"/api/v1/files/{nodeFileId}/content?download={download.ToString().ToLowerInvariant()}";
        return _transport.DownloadAsync(path, destination, authorize: true, progress, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? CreateIfMatchHeader(string? expectedETag)
    {
        if (string.IsNullOrWhiteSpace(expectedETag))
        {
            return null;
        }

        string trimmed = expectedETag.Trim();
        string headerValue = trimmed == "*" || trimmed.StartsWith('"')
            ? trimmed
            : "\"" + trimmed.Trim('"') + "\"";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IfMatchHeaderName] = headerValue,
        };
    }
}
