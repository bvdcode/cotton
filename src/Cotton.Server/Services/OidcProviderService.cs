// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services
{
    /// <summary>Manages administrator-configured OpenID Connect providers.</summary>
    public sealed partial class OidcProviderService(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
    {
        private static readonly string[] DefaultScopes = ["openid", "profile", "email"];
        private const int MaxSlugLength = 64;

        /// <summary>Lists enabled providers safe to show on the public login screen.</summary>
        public async Task<IReadOnlyList<PublicOidcProviderDto>> ListPublicAsync(CancellationToken ct)
        {
            var providers = await _dbContext.OidcProviders
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Name)
                .Select(x => new PublicOidcProviderDto
                {
                    Name = x.Name,
                    Slug = x.Slug
                })
                .ToListAsync(ct);

            return providers;
        }

        /// <summary>Lists all configured providers for administrators.</summary>
        public async Task<IReadOnlyList<OidcProviderDto>> ListAdminAsync(CancellationToken ct)
        {
            var providers = await _dbContext.OidcProviders
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            foreach (var provider in providers)
            {
                _integrity.RequireValid(_dbContext, provider, "oidc.admin-list");
            }

            return providers.Select(ToDto).ToArray();
        }

        /// <summary>Creates a provider.</summary>
        public async Task<OidcProviderDto> CreateAsync(OidcProviderRequestDto request, CancellationToken ct)
        {
            NormalizedProviderInput input = Normalize(request, requireSecret: false);
            string slug = await ResolveSlugAsync(input.Slug, input.Name, null, ct);

            var provider = new OidcProvider
            {
                Name = input.Name,
                Slug = slug,
                Issuer = input.Issuer,
                ClientId = input.ClientId,
                ClientSecretEncrypted = input.ClientSecret,
                Scopes = input.Scopes,
                IsEnabled = input.IsEnabled,
                AllowAccountCreation = input.AllowAccountCreation,
                RequireVerifiedEmail = input.RequireVerifiedEmail,
                DefaultRole = input.DefaultRole,
                AllowedEmailDomains = input.AllowedEmailDomains,
                SyncProfile = input.SyncProfile,
                SyncAvatar = input.SyncAvatar
            };

            await _dbContext.OidcProviders.AddAsync(provider, ct);
            await _dbContext.SaveChangesAsync(ct);
            return ToDto(provider);
        }

        /// <summary>Updates a provider.</summary>
        public async Task<OidcProviderDto> UpdateAsync(Guid providerId, OidcProviderRequestDto request, CancellationToken ct)
        {
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], ct)
                ?? throw new EntityNotFoundException<OidcProvider>();
            _integrity.RequireValid(_dbContext, provider, "oidc.admin-update");

            NormalizedProviderInput input = Normalize(request, requireSecret: false);
            provider.Name = input.Name;
            provider.Slug = await ResolveSlugAsync(input.Slug, input.Name, provider.Id, ct);
            provider.Issuer = input.Issuer;
            provider.ClientId = input.ClientId;
            if (input.ClearClientSecret)
            {
                provider.ClientSecretEncrypted = null;
            }
            else if (input.ClientSecret is not null)
            {
                provider.ClientSecretEncrypted = input.ClientSecret;
            }
            provider.Scopes = input.Scopes;
            provider.IsEnabled = input.IsEnabled;
            provider.AllowAccountCreation = input.AllowAccountCreation;
            provider.RequireVerifiedEmail = input.RequireVerifiedEmail;
            provider.DefaultRole = input.DefaultRole;
            provider.AllowedEmailDomains = input.AllowedEmailDomains;
            provider.SyncProfile = input.SyncProfile;
            provider.SyncAvatar = input.SyncAvatar;

            await _dbContext.SaveChangesAsync(ct);
            return ToDto(provider);
        }

        /// <summary>Deletes a provider and its links.</summary>
        public async Task DeleteAsync(Guid providerId, CancellationToken ct)
        {
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], ct)
                ?? throw new EntityNotFoundException<OidcProvider>();
            _integrity.RequireValid(_dbContext, provider, "oidc.admin-delete");
            _dbContext.OidcProviders.Remove(provider);
            await _dbContext.SaveChangesAsync(ct);
        }

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

        private async Task<string> ResolveSlugAsync(
            string? requestedSlug,
            string name,
            Guid? currentProviderId,
            CancellationToken ct)
        {
            string slug = string.IsNullOrWhiteSpace(requestedSlug)
                ? Slugify(name)
                : NormalizeSlug(requestedSlug);

            bool exists = await _dbContext.OidcProviders.AnyAsync(
                x => x.Slug == slug && x.Id != currentProviderId,
                ct);
            if (exists)
            {
                throw new BadRequestException<OidcProvider>("OIDC provider slug is already used.");
            }

            return slug;
        }

        private static NormalizedProviderInput Normalize(OidcProviderRequestDto request, bool requireSecret)
        {
            string name = RequiredTrim(request.Name, "Provider name is required.");
            string issuer = NormalizeIssuer(request.Issuer);
            string clientId = RequiredTrim(request.ClientId, "Client id is required.");
            string? clientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? null
                : request.ClientSecret.Trim();
            if (requireSecret && clientSecret is null)
            {
                throw new BadRequestException<OidcProvider>("Client secret is required.");
            }

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

        private static string Slugify(string value)
        {
            string lower = value.Trim().ToLowerInvariant();
            string normalized = SlugInvalidCharacters().Replace(lower, "-").Trim('-');
            if (normalized.Length == 0 || normalized[0] is < 'a' or > 'z')
            {
                normalized = $"oidc-{normalized}";
            }

            return normalized[..Math.Min(normalized.Length, MaxSlugLength)];
        }

        private static string NormalizeSlug(string value)
        {
            string slug = value.Trim().ToLowerInvariant();
            if (slug.Length is < UsernameValidator.MinLength or > MaxSlugLength || !SlugRegex().IsMatch(slug))
            {
                throw new BadRequestException<OidcProvider>(
                    "Slug must start with a letter and contain lowercase latin letters, digits, dots, dashes, or underscores.");
            }

            return slug;
        }

        private sealed record NormalizedProviderInput(
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
            bool SyncAvatar);

        [GeneratedRegex("[^a-z0-9._-]+", RegexOptions.CultureInvariant)]
        private static partial Regex SlugInvalidCharacters();

        [GeneratedRegex("^[a-z](?:[a-z0-9]|[._-](?=[a-z0-9])){1,63}$", RegexOptions.CultureInvariant)]
        private static partial Regex SlugRegex();
    }
}
