// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// External OpenID Connect identity linked to the current user.
    /// </summary>
    public class UserExternalIdentityDto : BaseDto<Guid>
    {
        /// <summary>
        /// Provider id.
        /// </summary>
        public Guid ProviderId { get; set; }

        /// <summary>
        /// Provider display name.
        /// </summary>
        public string ProviderName { get; set; } = null!;

        /// <summary>
        /// Provider slug.
        /// </summary>
        public string ProviderSlug { get; set; } = null!;

        /// <summary>
        /// Email reported by the provider.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Whether the provider verified the email.
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>
        /// Display name reported by the provider.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Profile picture URL reported by the provider.
        /// </summary>
        public string? PictureUrl { get; set; }

        /// <summary>
        /// UTC timestamp when this provider was last used.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }
    }
}
