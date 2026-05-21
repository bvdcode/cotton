// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("app_versions")]
    public class AppVersion : BaseEntity<Guid>
    {
        public string Version { get; set; } = null!;

        [Column("latest_release_version")]
        public string? LatestReleaseVersion { get; set; }

        [Column("latest_release_url")]
        public string? LatestReleaseUrl { get; set; }

        [Column("latest_release_checked_at")]
        public DateTime? LatestReleaseCheckedAt { get; set; }

        [Column("latest_release_notified_at")]
        public DateTime? LatestReleaseNotifiedAt { get; set; }
    }
}
