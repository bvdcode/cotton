// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>Runs OpenID Connect sign-in and account-linking flows.</summary>
    public class OidcAuthenticationService(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        SettingsProvider _settings,
        IPasswordHashService _hasher,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        AuthSessionIssuer _sessionIssuer,
        OidcAvatarImportService _avatarImporter,
        IDatabaseIntegrityVerifier _integrity)
    {
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);
        private const string CodeChallengeMethod = "S256";

        /// <summary>Starts a sign-in flow and returns the provider authorization URL.</summary>
        public Task<string> BeginSignInAsync(
            string providerSlug,
            string? returnUrl,
            bool trustDevice,
            CancellationToken ct)
        {
            return BeginAsync(providerSlug, returnUrl, trustDevice, linkUserId: null, ct);
        }

        /// <summary>Starts an account-linking flow and returns the provider authorization URL.</summary>
        public Task<string> BeginLinkAsync(
            Guid userId,
            string providerSlug,
            string? returnUrl,
            CancellationToken ct)
        {
            return BeginAsync(providerSlug, returnUrl, trustDevice: false, userId, ct);
        }

        /// <summary>Completes an authorization callback and returns an application return URL.</summary>
        public async Task<string> CompleteCallbackAsync(
            string state,
            string code,
            CancellationToken ct)
        {
            string stateHash = HashOpaqueValue(state);
            OidcLoginState loginState = await _dbContext.OidcLoginStates
                .Include(x => x.Provider)
                .FirstOrDefaultAsync(x => x.StateHash == stateHash, ct)
                ?? throw new BadRequestException<OidcLoginState>("OIDC sign-in state was not found.");
            _integrity.RequireValid(_dbContext, loginState, "oidc.callback-state");
            _integrity.RequireValid(_dbContext, loginState.Provider, "oidc.callback-provider");

            if (DateTime.UtcNow > loginState.ExpiresAt)
            {
                _dbContext.OidcLoginStates.Remove(loginState);
                await _dbContext.SaveChangesAsync(ct);
                throw new BadRequestException<OidcLoginState>("OIDC sign-in state has expired.");
            }

            if (!loginState.Provider.IsEnabled)
            {
                throw new BadRequestException<OidcProvider>("OIDC provider is disabled.");
            }

            OpenIdConnectConfiguration configuration = await _discovery.GetConfigurationAsync(loginState.Provider, ct);
            string redirectUri = await BuildRedirectUriAsync(ct);
            OidcTokenResponse tokenResponse = await _discovery.ExchangeCodeAsync(
                configuration,
                loginState.Provider,
                code,
                redirectUri,
                loginState.CodeVerifierEncrypted,
                ct);
            ClaimsPrincipal principal = ValidateIdToken(
                configuration,
                loginState.Provider,
                tokenResponse.IdToken,
                loginState.NonceEncrypted);
            OidcUserInfoClaims? userInfo = await _discovery.TryGetUserInfoAsync(
                configuration,
                tokenResponse.AccessToken,
                ct);
            OidcIdentityClaims claims = BuildIdentityClaims(
                loginState.Provider.Issuer,
                principal,
                userInfo);

            User user = loginState.LinkUserId.HasValue
                ? await LinkIdentityAsync(loginState.LinkUserId.Value, loginState.Provider, claims, ct)
                : await SignInOrCreateUserAsync(loginState.Provider, claims, ct);

            _dbContext.OidcLoginStates.Remove(loginState);
            await _dbContext.SaveChangesAsync(ct);

            if (!loginState.LinkUserId.HasValue)
            {
                await _sessionIssuer.SignInAsync(user, loginState.TrustDevice, AuthType.Credentials, ct);
            }

            return loginState.ReturnUrl;
        }

        /// <summary>Lists external identities linked to a user.</summary>
        public async Task<IReadOnlyList<UserExternalIdentityDto>> ListLinkedAsync(Guid userId, CancellationToken ct)
        {
            var identities = await _dbContext.UserExternalIdentities
                .Include(x => x.Provider)
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Provider.Name)
                .ToListAsync(ct);

            foreach (var identity in identities)
            {
                _integrity.RequireValid(_dbContext, identity, "oidc.link-list");
                _integrity.RequireValid(_dbContext, identity.Provider, "oidc.link-list-provider");
            }

            return identities.Select(ToDto).ToArray();
        }

        /// <summary>Unlinks an external identity from the current user.</summary>
        public async Task UnlinkAsync(Guid userId, Guid identityId, CancellationToken ct)
        {
            UserExternalIdentity identity = await _dbContext.UserExternalIdentities
                .FirstOrDefaultAsync(x => x.Id == identityId && x.UserId == userId, ct)
                ?? throw new EntityNotFoundException<UserExternalIdentity>();
            _integrity.RequireValid(_dbContext, identity, "oidc.unlink");
            await EnsureCanUnlinkAsync(userId, identityId, ct);
            _dbContext.UserExternalIdentities.Remove(identity);
            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task<string> BeginAsync(
            string providerSlug,
            string? returnUrl,
            bool trustDevice,
            Guid? linkUserId,
            CancellationToken ct)
        {
            await CleanupExpiredStatesAsync(ct);
            OidcProvider provider = await GetEnabledProviderAsync(providerSlug, ct);
            OpenIdConnectConfiguration configuration = await _discovery.GetConfigurationAsync(provider, ct);
            if (string.IsNullOrWhiteSpace(configuration.AuthorizationEndpoint))
            {
                throw new BadRequestException<OidcProvider>("OIDC provider does not publish an authorization endpoint.");
            }

            string state = CreateOpaqueValue();
            string codeVerifier = CreateOpaqueValue();
            string nonce = CreateOpaqueValue();
            string redirectUri = await BuildRedirectUriAsync(ct);
            var loginState = new OidcLoginState
            {
                ProviderId = provider.Id,
                StateHash = HashOpaqueValue(state),
                CodeVerifierEncrypted = codeVerifier,
                NonceEncrypted = nonce,
                ReturnUrl = NormalizeReturnUrl(returnUrl),
                LinkUserId = linkUserId,
                TrustDevice = trustDevice,
                ExpiresAt = DateTime.UtcNow.Add(StateLifetime)
            };

            await _dbContext.OidcLoginStates.AddAsync(loginState, ct);
            await _dbContext.SaveChangesAsync(ct);

            var parameters = new Dictionary<string, string?>
            {
                ["response_type"] = OpenIdConnectResponseType.Code,
                ["client_id"] = provider.ClientId,
                ["redirect_uri"] = redirectUri,
                ["scope"] = string.Join(' ', provider.Scopes),
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = CreateCodeChallenge(codeVerifier),
                ["code_challenge_method"] = CodeChallengeMethod
            };

            return QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, parameters);
        }

        private async Task<OidcProvider> GetEnabledProviderAsync(string providerSlug, CancellationToken ct)
        {
            string slug = providerSlug.Trim().ToLowerInvariant();
            OidcProvider provider = await _dbContext.OidcProviders
                .FirstOrDefaultAsync(x => x.Slug == slug, ct)
                ?? throw new EntityNotFoundException<OidcProvider>();
            _integrity.RequireValid(_dbContext, provider, "oidc.provider");

            if (!provider.IsEnabled)
            {
                throw new BadRequestException<OidcProvider>("OIDC provider is disabled.");
            }

            return provider;
        }

        private async Task<User> SignInOrCreateUserAsync(
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            UserExternalIdentity? identity = await _dbContext.UserExternalIdentities
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.ProviderId == provider.Id && x.Subject == claims.Subject, ct);
            if (identity is not null)
            {
                _integrity.RequireValid(_dbContext, identity, "oidc.signin-link");
                _integrity.RequireValid(_dbContext, identity.User, "oidc.signin-user");
                ApplyIdentityClaims(identity, claims);
                await ApplyUserSyncAsync(identity.User, provider, claims, ct);
                return identity.User;
            }

            if (!provider.AllowAccountCreation)
            {
                throw new BadRequestException<OidcProvider>(
                    "This provider can only sign in accounts that are already linked.");
            }

            ValidateAccountCreation(provider, claims);
            if (claims.Email is not null)
            {
                bool emailExists = await _dbContext.Users.AnyAsync(x => x.Email == claims.Email, ct);
                if (emailExists)
                {
                    throw new BadRequestException<User>(
                        "An account with this email already exists. Sign in normally and link this provider from profile settings.");
                }
            }

            string username = await BuildUsernameAsync(claims, ct);
            string randomSecret = CreateOpaqueValue();
            var user = new User
            {
                Username = username,
                Role = provider.DefaultRole,
                Email = claims.Email,
                IsEmailVerified = claims.EmailVerified,
                FirstName = claims.GivenName,
                LastName = claims.FamilyName,
                PasswordPhc = _hasher.Hash(randomSecret),
                WebDavTokenPhc = _hasher.Hash(randomSecret),
            };
            await _dbContext.Users.AddAsync(user, ct);
            var newIdentity = CreateIdentity(user.Id, provider.Id, provider.Issuer, claims);
            newIdentity.User = user;
            await _dbContext.UserExternalIdentities.AddAsync(newIdentity, ct);
            await TryImportUserAvatarAsync(user, provider, claims, ct);
            await _dbContext.SaveChangesAsync(ct);
            await _defaultUserContentSeeder.SeedAsync(user.Id);
            return user;
        }

        private async Task<User> LinkIdentityAsync(
            Guid userId,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "oidc.link-user");

            UserExternalIdentity? existingSubject = await _dbContext.UserExternalIdentities
                .FirstOrDefaultAsync(x => x.ProviderId == provider.Id && x.Subject == claims.Subject, ct);
            if (existingSubject is not null && existingSubject.UserId != userId)
            {
                throw new BadRequestException<UserExternalIdentity>(
                    "This external account is already linked to another Cotton account.");
            }

            UserExternalIdentity? existingProviderLink = await _dbContext.UserExternalIdentities
                .FirstOrDefaultAsync(x => x.ProviderId == provider.Id && x.UserId == userId, ct);
            if (existingProviderLink is not null)
            {
                _integrity.RequireValid(_dbContext, existingProviderLink, "oidc.link-existing-provider");
                if (existingProviderLink.Subject != claims.Subject)
                {
                    throw new BadRequestException<UserExternalIdentity>(
                        "This Cotton account is already linked to another account from the same provider.");
                }

                ApplyIdentityClaims(existingProviderLink, claims);
                await ApplyUserSyncAsync(user, provider, claims, ct);
                return user;
            }

            var identity = CreateIdentity(user.Id, provider.Id, provider.Issuer, claims);
            await _dbContext.UserExternalIdentities.AddAsync(identity, ct);
            await ApplyUserSyncAsync(user, provider, claims, ct);
            return user;
        }

        private static void ValidateAccountCreation(OidcProvider provider, OidcIdentityClaims claims)
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

        private async Task<string> BuildUsernameAsync(OidcIdentityClaims claims, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(claims.Email))
            {
                return await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(_dbContext, claims.Email, ct);
            }

            string fallback = claims.PreferredUsername ?? claims.Name ?? $"user-{claims.Subject[..Math.Min(8, claims.Subject.Length)]}";
            return await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(
                _dbContext,
                $"{fallback}@oidc.local",
                ct);
        }

        private static UserExternalIdentity CreateIdentity(
            Guid userId,
            Guid providerId,
            string issuer,
            OidcIdentityClaims claims)
        {
            var identity = new UserExternalIdentity
            {
                UserId = userId,
                ProviderId = providerId,
                Issuer = issuer,
                Subject = claims.Subject
            };
            ApplyIdentityClaims(identity, claims);
            return identity;
        }

        private static void ApplyIdentityClaims(UserExternalIdentity identity, OidcIdentityClaims claims)
        {
            identity.Email = claims.Email;
            identity.EmailVerified = claims.EmailVerified;
            identity.DisplayName = claims.Name;
            identity.PictureUrl = claims.PictureUrl;
            identity.LastUsedAt = DateTime.UtcNow;
        }

        private async Task ApplyUserSyncAsync(
            User user,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            ApplyProfileSync(user, provider, claims);
            await TryImportUserAvatarAsync(user, provider, claims, ct);
        }

        private async Task TryImportUserAvatarAsync(
            User user,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            if (!provider.SyncAvatar)
            {
                return;
            }

            await _avatarImporter.TryImportMissingAvatarAsync(user, claims.PictureUrl, ct);
        }

        private static void ApplyProfileSync(User user, OidcProvider provider, OidcIdentityClaims claims)
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

        private static ClaimsPrincipal ValidateIdToken(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider,
            string idToken,
            string nonce)
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration.Issuer ?? provider.Issuer,
                ValidateAudience = true,
                ValidAudience = provider.ClientId,
                ValidateLifetime = true,
                ClockSkew = ClockSkew,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                NameClaimType = "name"
            };

            ClaimsPrincipal principal = handler.ValidateToken(idToken, validationParameters, out SecurityToken token);
            if (token is not JwtSecurityToken jwt
                || string.Equals(jwt.Header.Alg, SecurityAlgorithms.None, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException<OidcProvider>("OIDC ID token signature is invalid.");
            }

            string? tokenNonce = principal.FindFirstValue("nonce");
            if (!string.Equals(tokenNonce, nonce, StringComparison.Ordinal))
            {
                throw new BadRequestException<OidcProvider>("OIDC ID token nonce is invalid.");
            }

            return principal;
        }

        private static OidcIdentityClaims BuildIdentityClaims(
            string expectedIssuer,
            ClaimsPrincipal principal,
            OidcUserInfoClaims? userInfo)
        {
            string subject = ReadRequiredClaim(
                principal,
                "OIDC subject is missing.",
                JwtRegisteredClaimNames.Sub,
                ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userInfo?.Subject)
                && !string.Equals(userInfo.Subject, subject, StringComparison.Ordinal))
            {
                throw new BadRequestException<OidcProvider>("OIDC user-info subject does not match the ID token.");
            }

            string issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss) ?? expectedIssuer;
            return new(
                issuer,
                subject,
                FirstNonEmpty(
                    userInfo?.Email,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Email),
                    principal.FindFirstValue("email"),
                    principal.FindFirstValue(ClaimTypes.Email)),
                userInfo?.EmailVerified ?? ReadBooleanClaim(principal, "email_verified"),
                FirstNonEmpty(
                    userInfo?.Name,
                    principal.FindFirstValue("name"),
                    principal.FindFirstValue(ClaimTypes.Name)),
                FirstNonEmpty(
                    userInfo?.GivenName,
                    principal.FindFirstValue("given_name"),
                    principal.FindFirstValue(ClaimTypes.GivenName)),
                FirstNonEmpty(
                    userInfo?.FamilyName,
                    principal.FindFirstValue("family_name"),
                    principal.FindFirstValue(ClaimTypes.Surname)),
                FirstNonEmpty(userInfo?.Picture, principal.FindFirstValue("picture")),
                FirstNonEmpty(userInfo?.PreferredUsername, principal.FindFirstValue("preferred_username")));
        }

        private static string ReadRequiredClaim(ClaimsPrincipal principal, string error, params string[] types)
        {
            string? value = types
                .Select(principal.FindFirstValue)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new BadRequestException<OidcProvider>(error);
            }

            return value.Trim();
        }

        private static bool ReadBooleanClaim(ClaimsPrincipal principal, string type)
        {
            string? value = principal.FindFirstValue(type);
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.Select(x => x?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private async Task<string> BuildRedirectUriAsync(CancellationToken ct)
        {
            string baseUrl = await _settings.GetPublicBaseUrlAsync(ct);
            return $"{baseUrl}{Routes.V1.Auth}/oidc/callback";
        }

        private static string NormalizeReturnUrl(string? returnUrl)
        {
            string trimmed = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl.Trim();
            return trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal)
                ? trimmed
                : "/";
        }

        private static string CreateOpaqueValue()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static string HashOpaqueValue(string value)
        {
            return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            return WebEncoders.Base64UrlEncode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier)));
        }

        private static UserExternalIdentityDto ToDto(UserExternalIdentity identity)
        {
            return new()
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
                LastUsedAt = identity.LastUsedAt
            };
        }

        private async Task EnsureCanUnlinkAsync(Guid userId, Guid identityId, CancellationToken ct)
        {
            bool hasAnotherExternalIdentity = await _dbContext.UserExternalIdentities
                .AnyAsync(x => x.UserId == userId && x.Id != identityId, ct);
            if (hasAnotherExternalIdentity)
            {
                return;
            }

            bool hasPasskey = await _dbContext.UserPasskeyCredentials
                .AnyAsync(x => x.UserId == userId, ct);
            if (hasPasskey)
            {
                return;
            }

            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "oidc.unlink-user");

            bool canResetPassword = user.IsEmailVerified
                && !string.IsNullOrWhiteSpace(user.Email)
                && _settings.GetServerSettings().EmailMode != EmailMode.None;
            if (canResetPassword)
            {
                return;
            }

            throw new BadRequestException<UserExternalIdentity>(
                "Add another sign-in method before unlinking the last external account.");
        }

        private Task CleanupExpiredStatesAsync(CancellationToken ct)
        {
            return _dbContext.OidcLoginStates
                .Where(x => x.ExpiresAt < DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
        }

        private record OidcIdentityClaims(
            string Issuer,
            string Subject,
            string? Email,
            bool EmailVerified,
            string? Name,
            string? GivenName,
            string? FamilyName,
            string? PictureUrl,
            string? PreferredUsername);
    }
}
