// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Stores the running Cotton version and latest release notification state.</summary>
    [Table("app_versions")]
    public class AppVersion : BaseEntity<Guid>
    {
        /// <summary>Current server version string.</summary>
        public string Version { get; set; } = null!;

        /// <summary>Latest upstream release version seen by the update checker.</summary>
        [Column("latest_release_version")]
        public string? LatestReleaseVersion { get; set; }

        /// <summary>URL of the latest upstream release.</summary>
        [Column("latest_release_url")]
        public string? LatestReleaseUrl { get; set; }

        /// <summary>UTC timestamp of the latest release check.</summary>
        [Column("latest_release_checked_at")]
        public DateTime? LatestReleaseCheckedAt { get; set; }

        /// <summary>UTC timestamp when admins were last notified about an available release.</summary>
        [Column("latest_release_notified_at")]
        public DateTime? LatestReleaseNotifiedAt { get; set; }
    }
}
