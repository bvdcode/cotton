// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Nodes;
using Cotton.Sync;
using Cotton.Sync.State;

namespace Cotton.Sync.Remote
{
    /// <summary>
    /// Crawls remote Cotton folders through the SDK node API.
    /// </summary>
    public class RemoteTreeCrawler : IRemoteTreeLookupCrawler, IRemotePathLookupCrawler
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

        /// <inheritdoc />
        public async Task<RemoteTreeLookupSnapshot> CrawlPathLookupsAsync(
            Guid rootNodeId,
            IReadOnlyCollection<string> relativePaths,
            IProgress<RemoteTreeScanProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(relativePaths);
            var snapshot = new RemoteTreeLookupSnapshot
            {
                RootNode = await _nodes.GetAsync(rootNodeId, cancellationToken).ConfigureAwait(false),
            };
            int directoriesScanned = 0;
            int filesScanned = 0;
            progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
            foreach (string relativePath in relativePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string normalizedPath = SyncPath.Normalize(relativePath);
                if (string.IsNullOrWhiteSpace(normalizedPath) || SyncPathIgnoreRules.ShouldIgnore(normalizedPath))
                {
                    continue;
                }

                RemotePathResolution resolution = await ResolvePathAsync(
                        snapshot.RootNode,
                        normalizedPath,
                        directory =>
                        {
                            if (TryAddDirectory(snapshot, directory))
                            {
                                directoriesScanned++;
                                ReportDirectoryScanProgress(progress, filesScanned, directoriesScanned, directory.RelativePath);
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (resolution.File is not null)
                {
                    if (TryAddFile(snapshot, resolution.File))
                    {
                        filesScanned++;
                        ReportScanProgress(progress, filesScanned, directoriesScanned, resolution.File.RelativePath);
                    }

                    continue;
                }

                if (resolution.Directory is not null)
                {
                    await CrawlCoreAsync(
                            resolution.Directory.Node.Id,
                            progress,
                            directory =>
                            {
                                if (TryAddDirectory(snapshot, directory))
                                {
                                    directoriesScanned++;
                                }
                            },
                            file =>
                            {
                                if (TryAddFile(snapshot, file))
                                {
                                    filesScanned++;
                                }
                            },
                            cancellationToken,
                            resolution.Directory.RelativePath)
                        .ConfigureAwait(false);
                }
            }

            progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
            return snapshot;
        }

        private async Task<NodeDto> CrawlCoreAsync(
            Guid rootNodeId,
            IProgress<RemoteTreeScanProgress>? progress,
            Action<RemoteDirectorySnapshot> addDirectory,
            Action<RemoteFileSnapshot> addFile,
            CancellationToken cancellationToken,
            string rootRelativePath = "")
        {
            ArgumentNullException.ThrowIfNull(addDirectory);
            ArgumentNullException.ThrowIfNull(addFile);
            NodeDto root = await _nodes.GetAsync(rootNodeId, cancellationToken).ConfigureAwait(false);
            var pending = new Stack<RemoteCrawlFrame>();
            pending.Push(new RemoteCrawlFrame(root, rootRelativePath, Page: 1, Loaded: 0));
            int directoriesScanned = 0;
            int filesScanned = 0;
            progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RemoteCrawlFrame frame = pending.Pop();
                NodeContentDto children = await _nodes.GetChildrenAsync(
                    frame.Node.Id,
                    frame.Page,
                    _pageSize,
                    depth: 0,
                    cancellationToken).ConfigureAwait(false);
                var childDirectories = new List<RemoteCrawlFrame>(children.Nodes.Count);
                foreach (NodeDto childNode in children.Nodes)
                {
                    string relativePath = Combine(frame.ParentPath, childNode.Name);
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
                    childDirectories.Add(new RemoteCrawlFrame(childNode, relativePath, Page: 1, Loaded: 0));
                }

                foreach (NodeFileManifestDto file in children.Files)
                {
                    string relativePath = Combine(frame.ParentPath, file.Name);
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
                int loaded = frame.Loaded + count;
                if (count != 0 && loaded < children.TotalCount)
                {
                    pending.Push(frame with { Page = frame.Page + 1, Loaded = loaded });
                }

                for (int index = childDirectories.Count - 1; index >= 0; index--)
                {
                    pending.Push(childDirectories[index]);
                }
            }

            progress?.Report(new RemoteTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
            return root;
        }

        private async Task<RemotePathResolution> ResolvePathAsync(
            NodeDto root,
            string relativePath,
            Action<RemoteDirectorySnapshot> addDirectory,
            CancellationToken cancellationToken)
        {
            string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            NodeDto currentNode = root;
            string currentPath = string.Empty;
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                NodeContentDto children = await FindChildPageContainingAsync(currentNode.Id, segment, cancellationToken).ConfigureAwait(false);
                NodeDto? childDirectory = children.Nodes.FirstOrDefault(node => string.Equals(node.Name, segment, StringComparison.OrdinalIgnoreCase));
                bool isLast = index == segments.Length - 1;
                if (isLast)
                {
                    string childPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
                    if (childDirectory is not null)
                    {
                        var directory = new RemoteDirectorySnapshot
                        {
                            RelativePath = childPath,
                            Node = childDirectory,
                        };
                        addDirectory(directory);
                        return RemotePathResolution.ForDirectory(directory);
                    }

                    NodeFileManifestDto? file = children.Files.FirstOrDefault(item => string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase));
                    if (file is not null)
                    {
                        return RemotePathResolution.ForFile(new RemoteFileSnapshot
                        {
                            RelativePath = childPath,
                            File = file,
                        });
                    }

                    return RemotePathResolution.NotFound;
                }

                if (childDirectory is null)
                {
                    return RemotePathResolution.NotFound;
                }

                currentPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
                addDirectory(new RemoteDirectorySnapshot
                {
                    RelativePath = currentPath,
                    Node = childDirectory,
                });
                currentNode = childDirectory;
            }

            return RemotePathResolution.NotFound;
        }

        private async Task<NodeContentDto> FindChildPageContainingAsync(
            Guid parentNodeId,
            string name,
            CancellationToken cancellationToken)
        {
            int page = 1;
            int loaded = 0;
            while (true)
            {
                NodeContentDto children = await _nodes.GetChildrenAsync(
                    parentNodeId,
                    page,
                    _pageSize,
                    depth: 0,
                    cancellationToken).ConfigureAwait(false);
                if (children.Nodes.Any(node => string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase))
                    || children.Files.Any(file => string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return children;
                }

                int count = children.Nodes.Count + children.Files.Count;
                loaded += count;
                if (count == 0 || loaded >= children.TotalCount)
                {
                    return children;
                }

                page++;
            }
        }

        private static bool TryAddDirectory(RemoteTreeLookupSnapshot snapshot, RemoteDirectorySnapshot directory)
        {
            return snapshot.DirectoriesByPath.TryAdd(SyncPath.ToKey(directory.RelativePath), directory);
        }

        private static bool TryAddFile(RemoteTreeLookupSnapshot snapshot, RemoteFileSnapshot file)
        {
            return snapshot.FilesByPath.TryAdd(SyncPath.ToKey(file.RelativePath), file);
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

        private readonly record struct RemoteCrawlFrame(NodeDto Node, string ParentPath, int Page, int Loaded);

        private sealed record RemotePathResolution(RemoteDirectorySnapshot? Directory, RemoteFileSnapshot? File)
        {
            public static RemotePathResolution NotFound { get; } = new(null, null);

            public static RemotePathResolution ForDirectory(RemoteDirectorySnapshot directory)
            {
                return new RemotePathResolution(directory, null);
            }

            public static RemotePathResolution ForFile(RemoteFileSnapshot file)
            {
                return new RemotePathResolution(null, file);
            }
        }
    }
}
