// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        [Column("username", TypeName = "citext")]
        public string Username { get; set; } = null!;

        [Column("password_phc")]
        public string? PasswordPhc { get; set; }

        [Column("role")]
        public UserRole Role { get; set; }
    }
}
