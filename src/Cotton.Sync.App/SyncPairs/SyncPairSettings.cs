// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{
    /// <summary>
    /// Represents one configured local-to-remote synchronization pair.
    /// </summary>
    public class SyncPairSettings
    {
        /// <summary>
        /// Gets or sets the stable local client identifier for this sync pair.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the user-facing sync pair name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the local root path synchronized by this pair.
        /// </summary>
        public string LocalRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote Cotton folder node identifier.
        /// </summary>
        public Guid RemoteRootNodeId { get; set; }

        /// <summary>
        /// Gets or sets the user-facing remote folder path.
        /// </summary>
        public string RemoteDisplayPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this pair should be synchronized.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the synchronization mode.
        /// </summary>
        public SyncPairMode Mode { get; set; } = SyncPairMode.FullMirror;

        /// <summary>
        /// Gets or sets when this pair was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this pair was last updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
