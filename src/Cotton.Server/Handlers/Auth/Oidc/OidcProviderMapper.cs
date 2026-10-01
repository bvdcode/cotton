// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    internal static class OidcProviderMapper
    {
        internal static OidcProviderDto ToDto(OidcProvider provider)
        {
            return new()
            {
                Id = provider.Id,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt,
                Name = provider.Name,
                Slug = provider.Slug,
                Issuer = provider.Issuer,
                ClientId = provider.ClientId,
                HasClientSecret = !string.IsNullOrWhiteSpace(provider.ClientSecretEncrypted),
                Scopes = provider.Scopes,
                IsEnabled = provider.IsEnabled,
                AllowAccountCreation = provider.AllowAccountCreation,
                RequireVerifiedEmail = provider.RequireVerifiedEmail,
                DefaultRole = provider.DefaultRole,
                AllowedEmailDomains = provider.AllowedEmailDomains,
                SyncProfile = provider.SyncProfile,
                SyncAvatar = provider.SyncAvatar
            };
        }
    }
}
