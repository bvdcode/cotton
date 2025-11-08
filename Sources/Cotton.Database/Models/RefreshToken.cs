using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Database.Models
{
    [Table("refresh_tokens")]
    [Index(nameof(Token), IsUnique = true)]
    public class RefreshToken : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("token")]
        public string Token { get; set; } = null!;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }
}
