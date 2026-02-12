using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("notifications")]
    public class Notification : BaseEntity<Guid>
    {
        [Column("title")]
        public string Title { get; set; } = null!;

        [Column("content")]
        public string? Content { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("priority")]
        public NotificationPriority Priority { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;
    }
}
