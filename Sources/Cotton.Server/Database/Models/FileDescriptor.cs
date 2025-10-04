using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Database.Models
{
    [Table("file_descriptors")]
    [Index(nameof(Blake3Hash), IsUnique = true)]
    public class FileDescriptor : BaseEntity<Guid>
    {
        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("size")]
        public long Size { get; set; }

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("blake3_hash")]
        public byte[] Blake3Hash { get; set; } = null!;
    }
}
