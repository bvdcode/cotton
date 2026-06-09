// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Sync.App.SyncPairs
{

    [Table("sync_pair_settings")]
    internal sealed class SyncPairSettingsEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(256)]
        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MaxLength(4096)]
        [Column("local_root_path")]
        public string LocalRootPath { get; set; } = string.Empty;

        [Column("remote_root_node_id")]
        public Guid RemoteRootNodeId { get; set; }

        [Required]
        [MaxLength(4096)]
        [Column("remote_display_path")]
        public string RemoteDisplayPath { get; set; } = string.Empty;

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("mode")]
        public SyncPairMode Mode { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }

        [Column("updated_at_utc")]
        public DateTime UpdatedAtUtc { get; set; }
    }
}
