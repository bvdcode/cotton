// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State
{
    /// <summary>
    /// Represents one baseline entry known to a sync pair.
    /// </summary>
    public sealed class SyncStateEntry
    {
        /// <summary>
        /// Gets or sets the sync pair identifier.
        /// </summary>
        public string SyncPairId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized relative path as it should be displayed.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the entry kind.
        /// </summary>
        public SyncEntryKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the last synced local content hash.
        /// </summary>
        public string? LocalContentHash { get; set; }

        /// <summary>
        /// Gets or sets the last synced local write timestamp.
        /// </summary>
        public DateTime? LocalLastWriteUtc { get; set; }

        /// <summary>
        /// Gets or sets the last synced local file size.
        /// </summary>
        public long? LocalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the remote folder identifier for directory entries.
        /// </summary>
        public Guid? RemoteNodeId { get; set; }

        /// <summary>
        /// Gets or sets the remote file identifier for file entries.
        /// </summary>
        public Guid? RemoteFileId { get; set; }

        /// <summary>
        /// Gets or sets the last synced remote content hash.
        /// </summary>
        public string? RemoteContentHash { get; set; }

        /// <summary>
        /// Gets or sets the last synced remote ETag.
        /// </summary>
        public string? RemoteETag { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this baseline was recorded.
        /// </summary>
        public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
