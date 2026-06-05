// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using Cotton.Sdk.Nodes;

namespace Cotton.Sync.Remote;

/// <summary>
/// Resolves or creates remote folder roots through the SDK node API.
/// </summary>
public sealed class RemoteRootResolver : IRemoteRootResolver
{
    private const int DefaultPageSize = 100;
    private readonly ICottonNodeClient _nodes;
    private readonly int _pageSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteRootResolver" /> class.
    /// </summary>
    public RemoteRootResolver(ICottonNodeClient nodes, int pageSize = DefaultPageSize)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        _pageSize = pageSize;
    }

    /// <inheritdoc />
    public async Task<NodeDto> EnsureAsync(string? remotePath = null, CancellationToken cancellationToken = default)
    {
        NodeDto current = await _nodes.ResolveAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return current;
        }

        string[] segments = remotePath.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            NodeDto? existing = await FindChildDirectoryAsync(current.Id, segment, cancellationToken).ConfigureAwait(false);
            current = existing ?? await _nodes.CreateAsync(current.Id, segment, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    private async Task<NodeDto?> FindChildDirectoryAsync(Guid parentNodeId, string name, CancellationToken cancellationToken)
    {
        int page = 1;
        int loaded = 0;
        while (true)
        {
            NodeContentDto content = await _nodes.GetChildrenAsync(
                parentNodeId,
                page,
                _pageSize,
                depth: 0,
                cancellationToken).ConfigureAwait(false);
            NodeDto? match = content.Nodes.FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            int count = content.Nodes.Count + content.Files.Count;
            loaded += count;
            if (count == 0 || loaded >= content.TotalCount)
            {
                return null;
            }

            page++;
        }
    }
}
