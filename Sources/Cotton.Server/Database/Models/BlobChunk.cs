using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("blob_chunks")]
    [Index(nameof(ChunkSha256))]
    [Index(nameof(BlobId), nameof(Order), IsUnique = true)]
    public class BlobChunk : BaseEntity<Guid>
    {
        [Column("chunk_order")]
        public int Order { get; set; } // 0..N-1

        [Column("blob_id")]
        public Guid BlobId { get; set; }

        [MaxLength(32)]
        [Column("chunk_sha256")]
        public byte[] ChunkSha256 { get; set; } = null!;

        public virtual Chunk Chunk { get; set; } = null!;
        public virtual Blob Blob { get; set; } = null!;
    }
}
