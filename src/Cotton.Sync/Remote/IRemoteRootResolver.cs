// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Resolves or creates a remote folder path for synchronization.
    /// </summary>
    public interface IRemoteRootResolver
    {
        /// <summary>
        /// Resolves the account root or creates missing folders along the specified path.
        /// </summary>
        Task<NodeDto> EnsureAsync(string? remotePath = null, CancellationToken cancellationToken = default);
    }
}
