// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Requests;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    internal partial record OidcProviderInput(
            string Name,
            string? Slug,
            string Issuer,
            string ClientId,
            string? ClientSecret,
            bool ClearClientSecret,
            string[] Scopes,
            bool IsEnabled,
            bool AllowAccountCreation,
            bool RequireVerifiedEmail,
            UserRole DefaultRole,
            string[] AllowedEmailDomains,
            bool SyncProfile,
            bool SyncAvatar)
    {
        private static readonly string[] DefaultScopes = ["openid", "profile", "email"];
        private const int MaxSlugLength = 64;

        public static OidcProviderInput Normalize(OidcProviderRequestDto request)
        {
            string name = RequiredTrim(request.Name, "Provider name is required.");
            string issuer = NormalizeIssuer(request.Issuer);
            string clientId = RequiredTrim(request.ClientId, "Client id is required.");
            string? clientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? null
                : request.ClientSecret.Trim();

            string[] scopes = NormalizeScopes(request.Scopes);
            string[] allowedEmailDomains = request.AllowedEmailDomains
                .Select(NormalizeEmailDomain)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            UserRole defaultRole = request.DefaultRole == UserRole.Admin
                ? throw new BadRequestException<OidcProvider>("OIDC auto-created accounts cannot default to admin.")
                : request.DefaultRole;

            return new(
                name,
                string.IsNullOrWhiteSpace(request.Slug) ? null : NormalizeSlug(request.Slug),
                issuer,
                clientId,
                clientSecret,
                request.ClearClientSecret,
                scopes,
                request.IsEnabled,
                request.AllowAccountCreation,
                request.RequireVerifiedEmail,
                defaultRole,
                allowedEmailDomains,
                request.SyncProfile,
                request.SyncAvatar);
        }

        private static string RequiredTrim(string value, string error)
        {
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw new BadRequestException<OidcProvider>(error);
            }

            return trimmed;
        }

        private static string NormalizeIssuer(string issuer)
        {
            string trimmed = RequiredTrim(issuer, "Issuer is required.").TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new BadRequestException<OidcProvider>("Issuer must be an absolute HTTPS URL.");
            }

            return trimmed;
        }

        private static string[] NormalizeScopes(string[] scopes)
        {
            string[] normalized = scopes
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (normalized.Length == 0)
            {
                normalized = DefaultScopes;
            }

            if (!normalized.Contains("openid", StringComparer.Ordinal))
            {
                normalized = ["openid", .. normalized];
            }

            return normalized;
        }

        private static string NormalizeEmailDomain(string domain)
        {
            return domain.Trim().TrimStart('@').ToLowerInvariant();
        }

        public static string Slugify(string value)
        {
            string lower = value.Trim().ToLowerInvariant();
            string normalized = SlugInvalidCharacters().Replace(lower, "-").Trim('-');
            if (normalized.Length == 0 || normalized[0] is < 'a' or > 'z')
            {
                normalized = $"oidc-{normalized}";
            }

            return normalized[..Math.Min(normalized.Length, MaxSlugLength)];
        }

        public static string NormalizeSlug(string value)
        {
            string slug = value.Trim().ToLowerInvariant();
            if (slug.Length is < UsernameValidator.MinLength or > MaxSlugLength || !SlugRegex().IsMatch(slug))
            {
                throw new BadRequestException<OidcProvider>(
                    "Slug must start with a letter and contain lowercase latin letters, digits, dots, dashes, or underscores.");
            }

            return slug;
        }

        [GeneratedRegex("[^a-z0-9._-]+", RegexOptions.CultureInvariant)]
        private static partial Regex SlugInvalidCharacters();

        [GeneratedRegex("^[a-z](?:[a-z0-9]|[._-](?=[a-z0-9])){1,63}$", RegexOptions.CultureInvariant)]
        private static partial Regex SlugRegex();
    }
}
