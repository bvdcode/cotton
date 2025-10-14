using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("file_manifests")]
    public class FileManifest : BaseEntity<Guid>
    {
        [Obsolete("Temporary empty")]
        [Column("owner_id")]
        public Guid? OwnerId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("sha256")]
        public byte[] Sha256 { get; set; } = null!;

        /// <summary>
        /// First ID is the first manifest created for a version, remains the same for all subsequent updates.
        /// </summary>
        [Column("version_stable_id")]
        public Guid VersionStableId { get; set; }

        public virtual User? Owner { get; set; } = null!;

        public virtual ICollection<FileManifest> FileManifests { get; set; } = [];
    }
}
