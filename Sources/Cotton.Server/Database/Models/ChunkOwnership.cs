using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("chunk_ownerships")]
    [Index(nameof(OwnerId), nameof(ChunkSha256), IsUnique = true)]
    public class ChunkOwnership : BaseOwnedEntity
    {
        [Column("chunk_sha256")]
        public byte[] ChunkSha256 { get; set; } = null!;
    }
}
