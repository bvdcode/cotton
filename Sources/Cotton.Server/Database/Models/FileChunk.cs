using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("file_chunks")]
    [Index(nameof(ChunkSha256))]
    [Index(nameof(FileDescriptorId), nameof(Order), IsUnique = true)]
    public class FileChunk
    {
        [Column("file_descriptor_id")]
        public Guid FileDescriptorId { get; set; }

        [Column("chunk_order")]
        public int Order { get; set; } // 0..N-1

        [Column("chunk_sha256", TypeName = "BINARY(32)")]
        public byte[] ChunkSha256 { get; set; } = null!;

        public virtual Chunk Chunk { get; set; } = null!;
        public virtual FileDescriptor FileDescriptor { get; set; } = null!;
    }
}
