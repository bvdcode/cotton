// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs;

/// <summary>
/// Checks whether a remote Cotton root node can be used by a sync pair.
/// </summary>
public interface IRemoteSyncRootProbe
{
    /// <summary>
    /// Returns true when the remote root node can be resolved.
    /// </summary>
    Task<bool> ExistsAsync(Guid remoteRootNodeId, CancellationToken cancellationToken = default);
}
