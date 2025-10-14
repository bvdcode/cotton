using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("user_layouts")]
    public class UserLayout : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<UserLayoutNode> UserLayoutNodes { get; set; } = [];
    }
}
