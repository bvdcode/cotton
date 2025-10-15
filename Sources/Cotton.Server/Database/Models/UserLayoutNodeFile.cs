using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("user_layout_node_files")]
    public class UserLayoutNodeFile : BaseEntity<Guid>
    {
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("user_layout_node_id")]
        public Guid UserLayoutNodeId { get; set; }

        public virtual FileManifest FileManifest { get; set; } = null!;
        public virtual UserLayoutNode UserLayoutNode { get; set; } = null!;
    }
}
