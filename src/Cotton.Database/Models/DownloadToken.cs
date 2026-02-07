using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Index(nameof(Token), IsUnique = true)]
    [Table("download_tokens")]
    public class DownloadToken : BaseEntity<Guid>
    {
        [Column("file_name")]
        public string FileName { get; set; } = null!;

        [Column("token")]
        public string Token { get; set; } = null!;

        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("created_by_user_id")]
        public Guid CreatedByUserId { get; set; }

        [Column("delete_after_use")]
        public bool DeleteAfterUse { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User CreatedByUser { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual FileManifest FileManifest { get; set; } = null!;
    }
}
