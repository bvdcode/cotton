using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("node_files")]
    [Index(nameof(FileManifestId), nameof(NodeId), IsUnique = true)]
    public class NodeFile : BaseEntity<Guid>
    {
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("node_id")]
        public Guid NodeId { get; set; }

        public virtual FileManifest FileManifest { get; set; } = null!;
        public virtual Node Node { get; set; } = null!;
    }
}
