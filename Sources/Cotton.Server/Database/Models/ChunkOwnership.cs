using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("chunk_ownerships")]
    public class ChunkOwnership : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("chunk_sha256")]
        public byte[] ChunkSha256 { get; set; } = null!;

        public virtual User Owner { get; set; } = null!;
    }
}
