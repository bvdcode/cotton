// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the update current user request payload accepted by the API.
    /// </summary>
    public class UpdateCurrentUserRequestDto
    {
        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        public string? Username { get; set; }

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
        /// Gets or sets avatar hash.
        /// </summary>
        public string? AvatarHash { get; set; }
    }
}
