// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    public class OidcProviderDto : BaseDto<Guid>
    {
        public string Name { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string Issuer { get; set; } = null!;

        public string ClientId { get; set; } = null!;

        public bool HasClientSecret { get; set; }

        public string[] Scopes { get; set; } = [];

        public bool IsEnabled { get; set; }

        public bool AllowAccountCreation { get; set; }

        public bool RequireVerifiedEmail { get; set; }

        public UserRole DefaultRole { get; set; }

        public string[] AllowedEmailDomains { get; set; } = [];

        public bool SyncProfile { get; set; }

        public bool SyncAvatar { get; set; }
    }
}
