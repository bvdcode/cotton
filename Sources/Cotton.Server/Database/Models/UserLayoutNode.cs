using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Abstractions;
using Cotton.Server.Database.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("user_layout_nodes")]
    [Index(nameof(UserLayoutId), nameof(Name), nameof(ParentId), nameof(Type), IsUnique = true)]
    public class UserLayoutNode : BaseOwnedEntity
    {
        [Column("user_layout_id")]
        public Guid UserLayoutId { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("type")]
        // TODO: make sure the parent node type is the same as this node type
        public UserLayoutNodeType Type { get; set; }

        public virtual UserLayout UserLayout { get; set; } = null!;
        public virtual UserLayoutNode? Parent { get; set; } = null!;
    }
}