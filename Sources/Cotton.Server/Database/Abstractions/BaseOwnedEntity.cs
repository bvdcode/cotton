using Cotton.Server.Database.Models;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Abstractions
{
    public abstract class BaseOwnedEntity : BaseEntity<Guid>
    {
        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        public virtual User Owner { get; set; } = null!;
    }
}
