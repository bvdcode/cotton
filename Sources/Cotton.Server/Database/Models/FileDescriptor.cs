using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("file_descriptors")]
    [Index(nameof(OwnerId), nameof(FileSha256), nameof(SizeBytes), IsUnique = true)]
    public class FileDescriptor : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("file_sha256", TypeName = "BINARY(32)")]
        public byte[] FileSha256 { get; set; } = null!;

        public ICollection<FileChunk> Chunks { get; set; } = [];
    }
}
