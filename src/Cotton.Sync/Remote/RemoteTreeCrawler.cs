// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Nodes;
using Cotton.Sync;
using Cotton.Sync.State;

namespace Cotton.Sync.Remote;

/// <summary>
/// Crawls remote Cotton folders through the SDK node API.
/// </summary>
public sealed class RemoteTreeCrawler : IRemoteTreeLookupCrawler
{
    private const int DefaultPageSize = 100;
    private const int ProgressReportItemInterval = 100;
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
        return await CrawlAsync(rootNodeId, progress: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RemoteTreeSnapshot> CrawlAsync(
        Guid rootNodeId,
        IProgress<RemoteTreeScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new RemoteTreeSnapshot();
        snapshot.RootNode = await CrawlCoreAsync(
                rootNodeId,
                progress,
                snapshot.Directories.Add,
                snapshot.Files.Add,
                cancellationToken)
            .ConfigureAwait(false);
        snapshot.Directories.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
        snapshot.Files.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<RemoteTreeLookupSnapshot> CrawlLookupsAsync(
        Guid rootNodeId,
        IProgress<RemoteTreeScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new RemoteTreeLookupSnapshot();
        snapshot.RootNode = await CrawlCoreAsync(
                rootNodeId,
                progress,
                directory => SyncPathLookup.Add(snapshot.DirectoriesByPath, directory, static item => item.RelativePath),
                file => SyncPathLookup.Add(snapshot.FilesByPath, file, static item => item.RelativePath),
                cancellationToken)
            .ConfigureAwait(false);
        return snapshot;
    }

    private async Task<NodeDto> CrawlCoreAsync(
        Guid rootNodeId,
        IProgress<RemoteTreeScanProgress>? progress,
        Action<RemoteDirectorySnapshot> addDirectory,
        Action<RemoteFileSnapshot> addFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addDirectory);
        ArgumentNullException.ThrowIfNull(addFile);
        NodeDto root = await _nodes.GetAsync(rootNodeId, cancellationToken).ConfigureAwait(false);
        var queue = new Queue<(NodeDto Node, string RelativePath)>();
        queue.Enqueue((root, string.Empty));
        int directoriesScanned = 0;
        int filesScanned = 0;
        progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));

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

                    addDirectory(new RemoteDirectorySnapshot
                    {
                        RelativePath = relativePath,
                        Node = childNode,
                    });
                    directoriesScanned++;
                    ReportDirectoryScanProgress(progress, filesScanned, directoriesScanned, relativePath);
                    queue.Enqueue((childNode, relativePath));
                }

                foreach (NodeFileManifestDto file in children.Files)
                {
                    string relativePath = Combine(parentPath, file.Name);
                    if (SyncPathIgnoreRules.ShouldIgnore(relativePath))
                    {
                        continue;
                    }

                    addFile(new RemoteFileSnapshot
                    {
                        RelativePath = relativePath,
                        File = file,
                    });
                    filesScanned++;
                    ReportScanProgress(progress, filesScanned, directoriesScanned, relativePath);
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

        progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
        return root;
    }

    private static void ReportScanProgress(
        IProgress<RemoteTreeScanProgress>? progress,
        int filesScanned,
        int directoriesScanned,
        string currentPath)
    {
        if (progress is null)
        {
            return;
        }

        if (filesScanned == 1 || filesScanned % ProgressReportItemInterval == 0)
        {
            progress.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath));
        }
    }

    private static void ReportDirectoryScanProgress(
        IProgress<RemoteTreeScanProgress>? progress,
        int filesScanned,
        int directoriesScanned,
        string currentPath)
    {
        if (progress is null)
        {
            return;
        }

        if (directoriesScanned == 1 || directoriesScanned % ProgressReportItemInterval == 0)
        {
            progress.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath));
        }
    }

    private static string Combine(string parentPath, string name)
    {
        string combined = string.IsNullOrWhiteSpace(parentPath)
            ? name
            : parentPath + "/" + name;
        return SyncPath.Normalize(combined);
    }

}
