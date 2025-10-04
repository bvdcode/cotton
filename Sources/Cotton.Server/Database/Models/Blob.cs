using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("blobs")]
    [Index(nameof(OwnerId), nameof(Parent))]
    [Index(nameof(OwnerId), nameof(Parent), nameof(Name), IsUnique = true)]
    [Index(nameof(OwnerId), nameof(FileSha256), nameof(SizeBytes), IsUnique = true)]
    public class Blob : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("parent")]
        public string Parent { get; set; } = null!;

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("file_sha256", TypeName = "BINARY(32)")]
        public byte[] FileSha256 { get; set; } = null!;

        public virtual ICollection<BlobChunk> BlobChunks { get; set; } = [];
    }
}
