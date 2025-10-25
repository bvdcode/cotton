using Cotton.Server.Database.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("layouts")]
    public class Layout : BaseOwnedEntity
    {
        public virtual ICollection<Node> Nodes { get; set; } = [];
    }
}
