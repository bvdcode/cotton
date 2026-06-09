// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{

    /// <summary>
    /// Describes one local-to-remote folder synchronization pair.
    /// </summary>
    public sealed class SyncPair
    {
        /// <summary>
        /// Gets or sets the stable sync pair identifier used by the state store.
        /// </summary>
        public string SyncPairId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the local folder path synchronized by this pair.
        /// </summary>
        public string LocalRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote Cotton node used as the synchronization root.
        /// </summary>
        public Guid RemoteRootNodeId { get; set; }
    }
}
