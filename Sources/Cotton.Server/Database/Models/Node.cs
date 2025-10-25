using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Abstractions;
using Cotton.Server.Database.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("nodes")]
    [Index(nameof(LayoutId), nameof(Name), nameof(ParentId), nameof(Type), IsUnique = true)]
    public class Node : BaseOwnedEntity
    {
        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("type")]
        // TODO: make sure the parent node type is the same as this node type
        public UserLayoutNodeType Type { get; set; }

        public virtual Layout Layout { get; set; } = null!;
        public virtual Node? Parent { get; set; } = null!;
        public virtual ICollection<Node> Children { get; set; } = [];
        public virtual ICollection<NodeFile> LayoutNodeFiles { get; set; } = [];
    }
}