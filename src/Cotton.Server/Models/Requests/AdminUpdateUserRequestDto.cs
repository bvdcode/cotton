// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Requests
{
    public class AdminUpdateUserRequestDto
    {
        [Required]
        [MinLength(2)]
        [MaxLength(32)]
        [RegularExpression("^[a-z][a-z0-9]{1,31}$")]
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateOnly? BirthDate { get; set; }
        public UserRole Role { get; set; } = UserRole.User;
    }
}
