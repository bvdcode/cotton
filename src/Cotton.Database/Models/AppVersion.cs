using EasyExtensions.EntityFrameworkCore.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Cotton.Database.Models
{
    [Table("app_versions")]
    public class AppVersion : BaseEntity<Guid>
    {
        public string Version { get; set; } = null!;
    }
}
