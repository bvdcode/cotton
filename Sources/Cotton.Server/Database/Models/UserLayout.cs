using Cotton.Server.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("user_layouts")]
    public class UserLayout : BaseOwnedEntity
    {
        public virtual ICollection<UserLayoutNode> UserLayoutNodes { get; set; } = [];
    }
}
