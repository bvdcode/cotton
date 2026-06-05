// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Nodes;
using Cotton.Sync.State;

namespace Cotton.Sync.Remote;

/// <summary>
/// Crawls remote Cotton folders through the SDK node API.
/// </summary>
public sealed class RemoteTreeCrawler : IRemoteTreeCrawler
{
    private const int DefaultPageSize = 100;
    private readonly ICottonNodeClient _nodes;
    private readonly int _pageSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteTreeCrawler" /> class.
    /// </summary>
    public RemoteTreeCrawler(ICottonNodeClient nodes, int pageSize = DefaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        _nodes = nodes;
        _pageSize = pageSize;
    }

    /// <inheritdoc />
    public async Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default)
    {
        NodeDto root = await _nodes.GetAsync(rootNodeId, cancellationToken).ConfigureAwait(false);
        var snapshot = new RemoteTreeSnapshot { RootNode = root };
        var queue = new Queue<(NodeDto Node, string RelativePath)>();
        queue.Enqueue((root, string.Empty));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (node, parentPath) = queue.Dequeue();
            int page = 1;
            int loaded = 0;
            while (true)
            {
                NodeContentDto children = await _nodes.GetChildrenAsync(
                    node.Id,
                    page,
                    _pageSize,
                    depth: 0,
                    cancellationToken).ConfigureAwait(false);
                foreach (NodeDto childNode in children.Nodes)
                {
                    string relativePath = Combine(parentPath, childNode.Name);
                    if (SyncPathIgnoreRules.ShouldIgnore(relativePath))
                    {
                        continue;
                    }

                    snapshot.Directories.Add(new RemoteDirectorySnapshot
                    {
                        RelativePath = relativePath,
                        Node = childNode,
                    });
                    queue.Enqueue((childNode, relativePath));
                }

                foreach (NodeFileManifestDto file in children.Files)
                {
                    string relativePath = Combine(parentPath, file.Name);
                    if (SyncPathIgnoreRules.ShouldIgnore(relativePath))
                    {
                        continue;
                    }

                    snapshot.Files.Add(new RemoteFileSnapshot
                    {
                        RelativePath = relativePath,
                        File = file,
                    });
                }

                int count = children.Nodes.Count + children.Files.Count;
                loaded += count;
                if (count == 0 || loaded >= children.TotalCount)
                {
                    break;
                }

                page++;
            }
        }

        snapshot.Directories.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
        snapshot.Files.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
        return snapshot;
    }

    private static string Combine(string parentPath, string name)
    {
        string combined = string.IsNullOrWhiteSpace(parentPath)
            ? name
            : parentPath + "/" + name;
        return SyncPath.Normalize(combined);
    }
}
