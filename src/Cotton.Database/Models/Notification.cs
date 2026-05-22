// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents one notification delivered to a user.</summary>
    [Table("notifications")]
    public class Notification : BaseEntity<Guid>
    {
        /// <summary>Notification title.</summary>
        [Column("title")]
        public string Title { get; set; } = null!;

        /// <summary>Notification body content.</summary>
        [Column("content")]
        public string? Content { get; set; }

        /// <summary>UTC timestamp when the user marked the notification as read.</summary>
        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        /// <summary>Extensible metadata associated with this row.</summary>
        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>Identifier of the user associated with this row.</summary>
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>Notification priority used for display and ordering.</summary>
        [Column("priority")]
        public NotificationPriority Priority { get; set; }

        /// <summary>Navigation property for the associated user.</summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;
    }
}
