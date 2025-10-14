using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("chunk_ownerships")]
    [Index(nameof(OwnerId), nameof(ChunkSha256), IsUnique = true)]
    public class ChunkOwnership : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("chunk_sha256")]
        public byte[] ChunkSha256 { get; set; } = null!;

        public virtual User Owner { get; set; } = null!;
    }
}
