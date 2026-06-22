// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Requests
{
    /// <summary>Request used to create or update an OpenID Connect provider.</summary>
    public sealed class OidcProviderRequestDto
    {
        /// <summary>Human-readable provider name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Optional stable URL-safe provider identifier.</summary>
        public string? Slug { get; set; }
        /// <summary>Issuer URL.</summary>
        public string Issuer { get; set; } = string.Empty;
        /// <summary>OAuth/OIDC client id.</summary>
        public string ClientId { get; set; } = string.Empty;
        /// <summary>OAuth/OIDC client secret. Null keeps the existing secret on update.</summary>
        public string? ClientSecret { get; set; }
        /// <summary>Whether an empty client secret should clear the stored secret on update.</summary>
        public bool ClearClientSecret { get; set; }
        /// <summary>Requested scopes. Empty means openid/profile/email.</summary>
        public string[] Scopes { get; set; } = [];
        /// <summary>Whether this provider is enabled for sign-in.</summary>
        public bool IsEnabled { get; set; }
        /// <summary>Whether unknown provider accounts may create Cotton accounts.</summary>
        public bool AllowAccountCreation { get; set; }
        /// <summary>Whether account creation requires provider-verified email.</summary>
        public bool RequireVerifiedEmail { get; set; } = true;
        /// <summary>Role assigned to automatically created accounts.</summary>
        public UserRole DefaultRole { get; set; } = UserRole.User;
        /// <summary>Allowed email domains for automatic account creation.</summary>
        public string[] AllowedEmailDomains { get; set; } = [];
        /// <summary>Whether profile names are synchronized on sign-in.</summary>
        public bool SyncProfile { get; set; } = true;
        /// <summary>Whether avatar URL is synchronized on sign-in.</summary>
        public bool SyncAvatar { get; set; } = true;
    }
}
