// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// Represents a local filesystem change under a configured sync root.
    /// </summary>
    public sealed record LocalSyncRootChange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSyncRootChange" /> class.
        /// </summary>
        public LocalSyncRootChange(Guid syncPairId, string fullPath, LocalSyncRootChangeKind kind)
        {
            ArgumentNullException.ThrowIfNull(fullPath);
            if (kind == LocalSyncRootChangeKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Local change kind must be known.");
            }

            SyncPairId = syncPairId;
            FullPath = fullPath;
            Kind = kind;
        }

        /// <summary>
        /// Gets the sync pair identifier.
        /// </summary>
        public Guid SyncPairId { get; }

        /// <summary>
        /// Gets the changed filesystem path.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the local filesystem change kind.
        /// </summary>
        public LocalSyncRootChangeKind Kind { get; }
    }
}
