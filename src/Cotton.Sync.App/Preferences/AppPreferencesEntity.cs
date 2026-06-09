// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Sync.App.Preferences
{

    [Table("app_preferences")]
    internal sealed class AppPreferencesEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [MaxLength(2048)]
        [Column("remembered_server_url")]
        public string? RememberedServerUrl { get; set; }

        [MaxLength(320)]
        [Column("remembered_username")]
        public string? RememberedUsername { get; set; }

        [Column("start_with_operating_system")]
        public bool StartWithOperatingSystem { get; set; }

        [Column("start_minimized_to_tray")]
        public bool StartMinimizedToTray { get; set; }

        [Column("enable_notifications")]
        public bool EnableNotifications { get; set; }

        [Column("is_sync_paused")]
        public bool IsSyncPaused { get; set; }

        [Column("theme_mode")]
        public AppThemeMode ThemeMode { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }

        [Column("updated_at_utc")]
        public DateTime UpdatedAtUtc { get; set; }
    }
}
