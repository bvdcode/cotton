// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Crawls remote Cotton folders while reporting remote scan progress.
    /// </summary>
    public interface IRemoteTreeProgressCrawler : IRemoteTreeCrawler
    {
        /// <summary>
        /// Crawls a remote root node recursively and reports discovered entries.
        /// </summary>
        Task<RemoteTreeSnapshot> CrawlAsync(
            Guid rootNodeId,
            IProgress<RemoteTreeScanProgress>? progress,
            CancellationToken cancellationToken = default);
    }
}
