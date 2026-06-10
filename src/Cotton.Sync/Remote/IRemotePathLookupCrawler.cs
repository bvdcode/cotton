// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{
    /// <summary>
    /// Crawls selected remote paths into lookups without walking unrelated remote folders.
    /// </summary>
    public interface IRemotePathLookupCrawler
    {
        /// <summary>
        /// Crawls selected relative paths and any remote descendants under directory paths.
        /// </summary>
        Task<RemoteTreeLookupSnapshot> CrawlPathLookupsAsync(
            Guid rootNodeId,
            IReadOnlyCollection<string> relativePaths,
            IProgress<RemoteTreeScanProgress>? progress,
            CancellationToken cancellationToken = default);
    }
}
