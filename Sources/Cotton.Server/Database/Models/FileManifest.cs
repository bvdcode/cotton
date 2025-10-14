using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("file_manifests")]
    [Index(nameof(OwnerId), nameof(Folder))]
    [Index(nameof(OwnerId), nameof(Folder), nameof(Name), IsUnique = true)]
    public class FileManifest : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid? OwnerId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("folder")]
        public string Folder { get; set; } = null!;

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("sha256")]
        public byte[] Sha256 { get; set; } = null!;

        public virtual User? Owner { get; set; } = null!;

        public virtual ICollection<FileManifest> FileManifests { get; set; } = [];
    }
}
