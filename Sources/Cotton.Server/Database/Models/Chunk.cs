using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("chunks")]
    public class Chunk
    {
        [Key]
        [MaxLength(32)]
        [Column("sha256")]
        public byte[] Sha256 { get; set; } = null!;
    }
}
