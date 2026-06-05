// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Nodes;

/// <summary>
/// Provides Cotton node and folder operations used by synchronization clients.
/// </summary>
public sealed class CottonNodeClient : ICottonNodeClient
{
    private readonly CottonHttpTransport _transport;

    internal CottonNodeClient(CottonHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Resolves the latest layout root or a descendant path.
    /// </summary>
    public Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        string route = string.IsNullOrWhiteSpace(path)
            ? "/api/v1/layouts/resolver"
            : "/api/v1/layouts/resolver/" + EncodePath(path);
        return _transport.SendJsonAsync<NodeDto>(HttpMethod.Get, route, cancellationToken: cancellationToken);
    }

    private static string EncodePath(string path)
    {
        string[] segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", segments.Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Gets a node by identifier.
    /// </summary>
    public Task<NodeDto> GetAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeDto>(HttpMethod.Get, $"/api/v1/layouts/nodes/{nodeId}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets one page of child nodes and files.
    /// </summary>
    public Task<NodeContentDto> GetChildrenAsync(
        Guid nodeId,
        int page = 1,
        int pageSize = 100,
        int depth = 0,
        CancellationToken cancellationToken = default)
    {
        string route = $"/api/v1/layouts/nodes/{nodeId}/children?page={page}&pageSize={pageSize}&depth={depth}";
        return _transport.SendJsonAsync<NodeContentDto>(HttpMethod.Get, route, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a child node under the specified parent.
    /// </summary>
    public Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _transport.SendJsonAsync<NodeDto>(
            HttpMethod.Put,
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = parentId, Name = name.Trim() },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves a node under a different parent node.
    /// </summary>
    public Task<NodeDto> MoveAsync(Guid nodeId, Guid parentId, CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeDto>(
            HttpMethod.Patch,
            $"/api/v1/layouts/nodes/{nodeId}/move",
            new MoveNodeRequestDto { ParentId = parentId },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Renames a node.
    /// </summary>
    public Task<NodeDto> RenameAsync(Guid nodeId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _transport.SendJsonAsync<NodeDto>(
            HttpMethod.Patch,
            $"/api/v1/layouts/nodes/{nodeId}/rename",
            new RenameNodeRequestDto { Name = name.Trim() },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Merges metadata into a node.
    /// </summary>
    public Task<NodeDto> UpdateMetadataAsync(
        Guid nodeId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return _transport.SendJsonAsync<NodeDto>(
            HttpMethod.Patch,
            $"/api/v1/layouts/nodes/{nodeId}/metadata",
            new Dictionary<string, string>(metadata),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes a node.
    /// </summary>
    public Task DeleteAsync(Guid nodeId, bool skipTrash = false, CancellationToken cancellationToken = default)
    {
        return _transport.SendNoContentAsync(
            HttpMethod.Delete,
            $"/api/v1/layouts/nodes/{nodeId}?skipTrash={skipTrash.ToString().ToLowerInvariant()}",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Restores a trashed node.
    /// </summary>
    public Task<NodeDto> RestoreAsync(
        Guid nodeId,
        RestoreItemRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<NodeDto>(
            HttpMethod.Post,
            $"/api/v1/layouts/nodes/{nodeId}/restore",
            request ?? new RestoreItemRequestDto(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets ancestor nodes for a node.
    /// </summary>
    public Task<List<NodeDto>> GetAncestorsAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<List<NodeDto>>(
            HttpMethod.Get,
            $"/api/v1/layouts/nodes/{nodeId}/ancestors",
            cancellationToken: cancellationToken);
    }
}
