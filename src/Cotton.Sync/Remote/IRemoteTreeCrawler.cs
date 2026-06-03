// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote;

/// <summary>
/// Crawls remote Cotton folders into a snapshot usable by the sync engine.
/// </summary>
public interface IRemoteTreeCrawler
{
    /// <summary>
    /// Crawls a remote root node recursively.
    /// </summary>
    Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default);
}
