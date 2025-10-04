using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("chunks")]
    [Index(nameof(Sha256), IsUnique = true)]
    public class Chunk
    {
        [Key]
        [Column("id")]
        public Guid Id { get; protected set; }

        [Column("sha256", TypeName = "BINARY(32)")]
        public byte[] Sha256 { get; set; } = null!;
    }
}
