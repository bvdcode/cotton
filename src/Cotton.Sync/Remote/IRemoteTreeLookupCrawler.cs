// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Crawls remote Cotton folders into path lookups without retaining an intermediate tree list.
    /// </summary>
    public interface IRemoteTreeLookupCrawler : IRemoteTreeProgressCrawler
    {
        /// <summary>
        /// Crawls a remote root node recursively and returns file and directory lookups.
        /// </summary>
        Task<RemoteTreeLookupSnapshot> CrawlLookupsAsync(
            Guid rootNodeId,
            IProgress<RemoteTreeScanProgress>? progress,
            CancellationToken cancellationToken = default);
    }
}
