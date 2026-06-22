// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Public provider option shown on the login page.
    /// </summary>
    public class PublicOidcProviderDto
    {
        /// <summary>
        /// Provider display name.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Stable URL-safe provider identifier.
        /// </summary>
        public string Slug { get; set; } = null!;
    }
}
