// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Requests
{
    public class OidcProviderRequestDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public string Issuer { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string? ClientSecret { get; set; }

        public bool ClearClientSecret { get; set; }

        public string[] Scopes { get; set; } = [];

        public bool IsEnabled { get; set; }

        public bool AllowAccountCreation { get; set; }

        public bool RequireVerifiedEmail { get; set; } = true;

        public UserRole DefaultRole { get; set; } = UserRole.User;

        public string[] AllowedEmailDomains { get; set; } = [];

        public bool SyncProfile { get; set; } = true;

        public bool SyncAvatar { get; set; } = true;
    }
}
