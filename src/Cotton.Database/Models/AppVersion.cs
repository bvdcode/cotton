using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("app_versions")]
    public class AppVersion : BaseEntity<Guid>
    {
        public string Version { get; set; } = null!;
    }
}
