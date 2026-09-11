// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Services
{
    internal static class OidcAccountPolicy
    {
        public static void ValidateCreation(OidcProvider provider, OidcIdentityClaims claims)
        {
            if (provider.RequireVerifiedEmail && !claims.EmailVerified)
            {
                throw new BadRequestException<OidcProvider>(
                    "This provider requires a verified email address to create an account.");
            }

            if (provider.AllowedEmailDomains.Length == 0)
            {
                return;
            }

            string? domain = claims.Email?.Split('@', 2).ElementAtOrDefault(1)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(domain)
                || !provider.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                throw new BadRequestException<OidcProvider>(
                    "This provider cannot create accounts for the supplied email domain.");
            }
        }
    }
}
