// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
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
using System.Security.Claims;

namespace Cotton.Server.Services
{
    public class OidcAuthenticationService(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        SettingsProvider _settings,
        IPasswordHashService _hasher,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        AuthSessionIssuer _sessionIssuer,
        OidcAvatarImportService _avatarImporter,
        IDatabaseIntegrityVerifier _integrity,
        INotificationsProvider _notifications,
        ILogger<OidcAuthenticationService> _logger)
    {
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
        private const string CodeChallengeMethod = "S256";

        public Task<string> BeginSignInAsync(
            string providerSlug,
            string? returnUrl,
            bool trustDevice,
            CancellationToken ct)
        {
            return BeginAsync(providerSlug, returnUrl, trustDevice, linkUserId: null, ct);
        }

        public Task<string> BeginLinkAsync(
            Guid userId,
            string providerSlug,
            string? returnUrl,
            CancellationToken ct)
        {
            return BeginAsync(providerSlug, returnUrl, trustDevice: false, userId, ct);
        }

        public async Task<string> CompleteCallbackAsync(
            string state,
            string code,
            CancellationToken ct)
        {
            string stateHash = OidcProtocol.HashOpaqueValue(state);
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
            ClaimsPrincipal principal = OidcProtocol.ValidateIdToken(
                configuration,
                loginState.Provider,
                tokenResponse.IdToken,
                loginState.NonceEncrypted);
            OidcUserInfoClaims? userInfo = await _discovery.TryGetUserInfoAsync(
                configuration,
                tokenResponse.AccessToken,
                ct);
            OidcIdentityClaims claims = OidcProtocol.CreateClaims(
                loginState.Provider.Issuer,
                principal,
                userInfo);

            bool isLinkFlow = loginState.LinkUserId.HasValue;
            string? securityEmailRecipient = null;
            User user;
            if (loginState.LinkUserId is Guid linkUserId)
            {
                (user, securityEmailRecipient) = await LinkIdentityAsync(linkUserId, loginState.Provider, claims, ct);
            }
            else
            {
                user = await SignInOrCreateUserAsync(loginState.Provider, claims, ct);
            }

            _dbContext.OidcLoginStates.Remove(loginState);
            await _dbContext.SaveChangesAsync(ct);

            if (isLinkFlow)
            {
                await _notifications.SendSecurityEmailAsync(
                    _settings,
                    _logger,
                    user.Id,
                    NotificationTemplates.ExternalIdentityLinkedTitle,
                    NotificationTemplates.ExternalIdentityLinkedContent(loginState.Provider.Name),
                    DateTime.UtcNow,
                    securityEmailRecipient);
            }
            else
            {
                await _sessionIssuer.SignInAsync(user, loginState.TrustDevice, AuthType.Credentials, ct);
            }

            return loginState.ReturnUrl;
        }

        public async Task<IReadOnlyList<UserExternalIdentityDto>> ListLinkedAsync(Guid userId, CancellationToken ct)
        {
            List<UserExternalIdentity> identities = await _dbContext.UserExternalIdentities
                .Include(x => x.Provider)
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Provider.Name)
                .ToListAsync(ct);

            foreach (UserExternalIdentity? identity in identities)
            {
                _integrity.RequireValid(_dbContext, identity, "oidc.link-list");
                _integrity.RequireValid(_dbContext, identity.Provider, "oidc.link-list-provider");
            }

            return identities.Select(OidcIdentityMapper.ToDto).ToArray();
        }

        public async Task UnlinkAsync(Guid userId, Guid identityId, CancellationToken ct)
        {
            UserExternalIdentity identity = await _dbContext.UserExternalIdentities
                .Include(x => x.Provider)
                .FirstOrDefaultAsync(x => x.Id == identityId && x.UserId == userId, ct)
                ?? throw new EntityNotFoundException<UserExternalIdentity>();
            _integrity.RequireValid(_dbContext, identity, "oidc.unlink");
            await EnsureCanUnlinkAsync(userId, identityId, ct);
            _dbContext.UserExternalIdentities.Remove(identity);
            await _dbContext.SaveChangesAsync(ct);
            await _notifications.SendSecurityEmailAsync(
                _settings,
                _logger,
                userId,
                NotificationTemplates.ExternalIdentityUnlinkedTitle,
                NotificationTemplates.ExternalIdentityUnlinkedContent(identity.Provider.Name),
                DateTime.UtcNow);
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

            string state = OidcProtocol.CreateOpaqueValue();
            string codeVerifier = OidcProtocol.CreateOpaqueValue();
            string nonce = OidcProtocol.CreateOpaqueValue();
            string redirectUri = await BuildRedirectUriAsync(ct);
            OidcLoginState loginState = new OidcLoginState
            {
                ProviderId = provider.Id,
                StateHash = OidcProtocol.HashOpaqueValue(state),
                CodeVerifierEncrypted = codeVerifier,
                NonceEncrypted = nonce,
                ReturnUrl = OidcProtocol.NormalizeReturnUrl(returnUrl),
                LinkUserId = linkUserId,
                TrustDevice = trustDevice,
                ExpiresAt = DateTime.UtcNow.Add(StateLifetime)
            };

            await _dbContext.OidcLoginStates.AddAsync(loginState, ct);
            await _dbContext.SaveChangesAsync(ct);

            Dictionary<string, string?> parameters = new Dictionary<string, string?>
            {
                ["response_type"] = OpenIdConnectResponseType.Code,
                ["client_id"] = provider.ClientId,
                ["redirect_uri"] = redirectUri,
                ["scope"] = string.Join(' ', provider.Scopes),
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = OidcProtocol.CreateCodeChallenge(codeVerifier),
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
                OidcIdentityMapper.ApplyClaims(identity, claims);
                await ApplyUserSyncAsync(identity.User, provider, claims, ct);
                return identity.User;
            }

            if (!provider.AllowAccountCreation)
            {
                throw new BadRequestException<OidcProvider>(
                    "This provider can only sign in accounts that are already linked.");
            }

            OidcAccountPolicy.ValidateCreation(provider, claims);
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
            string randomSecret = OidcProtocol.CreateOpaqueValue();
            User user = new User
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
            UserExternalIdentity newIdentity = OidcIdentityMapper.Create(user.Id, provider.Id, provider.Issuer, claims);
            newIdentity.User = user;
            await _dbContext.UserExternalIdentities.AddAsync(newIdentity, ct);
            await TryImportUserAvatarAsync(user, provider, claims, ct);
            await _dbContext.SaveChangesAsync(ct);
            await _defaultUserContentSeeder.SeedAsync(user.Id);
            return user;
        }

        private async Task<(User User, string? PreviousEmail)> LinkIdentityAsync(
            Guid userId,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "oidc.link-user");
            string? previousEmail = user.Email;

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

                OidcIdentityMapper.ApplyClaims(existingProviderLink, claims);
                await ApplyUserSyncAsync(user, provider, claims, ct);
                return (user, previousEmail);
            }

            UserExternalIdentity identity = OidcIdentityMapper.Create(user.Id, provider.Id, provider.Issuer, claims);
            await _dbContext.UserExternalIdentities.AddAsync(identity, ct);
            await ApplyUserSyncAsync(user, provider, claims, ct);
            return (user, previousEmail);
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

        private async Task ApplyUserSyncAsync(
            User user,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken ct)
        {
            OidcIdentityMapper.ApplyProfile(user, provider, claims);
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

            await _avatarImporter.TryImportMissingAvatarAsync(
                user,
                claims.PictureUrl,
                provider.Issuer,
                ct);
        }

        private async Task<string> BuildRedirectUriAsync(CancellationToken ct)
        {
            string baseUrl = await _settings.GetPublicBaseUrlAsync(ct);
            return $"{baseUrl}{Routes.V1.Auth}/oidc/callback";
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

    }
}
