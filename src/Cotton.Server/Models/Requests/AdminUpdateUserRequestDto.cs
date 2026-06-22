// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the admin update user request payload accepted by the API.
    /// </summary>
    public class AdminUpdateUserRequestDto
    {
        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        [Required]
        public string Username { get; set; } = null!;

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Indicates whether email verified.
        /// </summary>
        public bool IsEmailVerified { get; set; }

        /// <summary>
        /// Gets or sets first name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Gets or sets last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets birth date.
        /// </summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        public UserRole Role { get; set; } = UserRole.User;
    }
}
