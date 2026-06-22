// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    /// <summary>Administrator-visible OpenID Connect provider settings.</summary>
    public class OidcProviderDto : BaseDto<Guid>
    {
        /// <summary>Human-readable provider name.</summary>
        public string Name { get; set; } = null!;
        /// <summary>Stable URL-safe provider identifier.</summary>
        public string Slug { get; set; } = null!;
        /// <summary>Issuer URL.</summary>
        public string Issuer { get; set; } = null!;
        /// <summary>Client id.</summary>
        public string ClientId { get; set; } = null!;
        /// <summary>Whether a client secret is configured.</summary>
        public bool HasClientSecret { get; set; }
        /// <summary>Requested scopes.</summary>
        public string[] Scopes { get; set; } = [];
        /// <summary>Whether this provider is enabled for sign-in.</summary>
        public bool IsEnabled { get; set; }
        /// <summary>Whether unknown provider accounts may create Cotton accounts.</summary>
        public bool AllowAccountCreation { get; set; }
        /// <summary>Whether account creation requires provider-verified email.</summary>
        public bool RequireVerifiedEmail { get; set; }
        /// <summary>Role assigned to automatically created accounts.</summary>
        public UserRole DefaultRole { get; set; }
        /// <summary>Allowed email domains for automatic account creation.</summary>
        public string[] AllowedEmailDomains { get; set; } = [];
        /// <summary>Whether profile names are synchronized on sign-in.</summary>
        public bool SyncProfile { get; set; }
        /// <summary>Whether avatar URL is synchronized on sign-in.</summary>
        public bool SyncAvatar { get; set; }
    }
}
