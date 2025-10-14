using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("password_phc")]
        public string? PasswordPhc { get; set; }
    }
}
