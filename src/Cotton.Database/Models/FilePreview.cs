using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("file_previews")]
    [Index(nameof(Hash), IsUnique = true)]
    public class FilePreview : BaseEntity<Guid>
    {
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;
    }
}
