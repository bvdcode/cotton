// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.State
{
    /// <summary>
    /// Represents one persisted synchronization baseline row.
    /// </summary>
    [Table("sync_entries")]
    [Index(nameof(SyncPairId), nameof(RelativePathKey), IsUnique = true)]
    [Index(nameof(RemoteFileId))]
    [Index(nameof(RemoteNodeId))]
    public sealed class SyncStateEntity
    {
        /// <summary>
        /// Gets or sets the database row identifier.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the sync pair identifier.
        /// </summary>
        [Required]
        [MaxLength(256)]
        [Column("sync_pair_id")]
        public string SyncPairId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the case-insensitive normalized relative path key.
        /// </summary>
        [Required]
        [MaxLength(1024)]
        [Column("relative_path_key")]
        public string RelativePathKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display relative path.
        /// </summary>
        [Required]
        [MaxLength(1024)]
        [Column("relative_path")]
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the entry kind.
        /// </summary>
        [Column("kind")]
        public SyncEntryKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the last synced local content hash.
        /// </summary>
        [MaxLength(128)]
        [Column("local_content_hash")]
        public string? LocalContentHash { get; set; }

        /// <summary>
        /// Gets or sets the last synced local write timestamp.
        /// </summary>
        [Column("local_last_write_utc")]
        public DateTime? LocalLastWriteUtc { get; set; }

        /// <summary>
        /// Gets or sets the last synced local file size.
        /// </summary>
        [Column("local_size_bytes")]
        public long? LocalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the remote folder identifier for directory entries.
        /// </summary>
        [Column("remote_node_id")]
        public Guid? RemoteNodeId { get; set; }

        /// <summary>
        /// Gets or sets the remote file identifier for file entries.
        /// </summary>
        [Column("remote_file_id")]
        public Guid? RemoteFileId { get; set; }

        /// <summary>
        /// Gets or sets the last synced remote content hash.
        /// </summary>
        [MaxLength(128)]
        [Column("remote_content_hash")]
        public string? RemoteContentHash { get; set; }

        /// <summary>
        /// Gets or sets the last synced remote ETag.
        /// </summary>
        [MaxLength(256)]
        [Column("remote_etag")]
        public string? RemoteETag { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this baseline was recorded.
        /// </summary>
        [Column("synced_at_utc")]
        public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
