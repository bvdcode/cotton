// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services
{
    internal static class OidcIdentityMapper
    {
        public static UserExternalIdentity Create(
            Guid userId,
            Guid providerId,
            string issuer,
            OidcIdentityClaims claims)
        {
            UserExternalIdentity identity = new()
            {
                UserId = userId,
                ProviderId = providerId,
                Issuer = issuer,
                Subject = claims.Subject,
            };
            ApplyClaims(identity, claims);
            return identity;
        }

        public static void ApplyClaims(UserExternalIdentity identity, OidcIdentityClaims claims)
        {
            identity.Email = claims.Email;
            identity.EmailVerified = claims.EmailVerified;
            identity.DisplayName = claims.Name;
            identity.PictureUrl = claims.PictureUrl;
            identity.LastUsedAt = DateTime.UtcNow;
        }

        public static void ApplyProfile(User user, OidcProvider provider, OidcIdentityClaims claims)
        {
            if (!provider.SyncProfile)
            {
                return;
            }

            user.FirstName = claims.GivenName ?? user.FirstName;
            user.LastName = claims.FamilyName ?? user.LastName;
            if (claims.EmailVerified && !string.IsNullOrWhiteSpace(claims.Email))
            {
                user.Email = claims.Email;
                user.IsEmailVerified = true;
            }
        }

        public static UserExternalIdentityDto ToDto(UserExternalIdentity identity)
        {
            return new UserExternalIdentityDto
            {
                Id = identity.Id,
                CreatedAt = identity.CreatedAt,
                UpdatedAt = identity.UpdatedAt,
                ProviderId = identity.ProviderId,
                ProviderName = identity.Provider.Name,
                ProviderSlug = identity.Provider.Slug,
                Email = identity.Email,
                EmailVerified = identity.EmailVerified,
                DisplayName = identity.DisplayName,
                PictureUrl = identity.PictureUrl,
                LastUsedAt = identity.LastUsedAt,
            };
        }
    }
}
