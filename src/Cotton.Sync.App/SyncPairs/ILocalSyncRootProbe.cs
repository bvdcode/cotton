// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs;

/// <summary>
/// Checks whether a local sync root can be used by the desktop client.
/// </summary>
public interface ILocalSyncRootProbe
{
    /// <summary>
    /// Returns true when the local root exists or can be created and accessed.
    /// </summary>
    Task<bool> CanUseAsync(string localRootPath, CancellationToken cancellationToken = default);
}
