using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("layout_node_files")]
    public class LayoutNodeFile : BaseEntity<Guid>
    {
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("node_id")]
        public Guid NodeId { get; set; }

        public virtual FileManifest FileManifest { get; set; } = null!;
        public virtual Node Node { get; set; } = null!;
    }
}
