// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record BeginOidcAuthenticationRequest(string ProviderSlug, string? ReturnUrl, bool TrustDevice, Guid? LinkUserId = null) : IRequest<string>;

    public class BeginOidcAuthenticationRequestHandler(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        SettingsProvider _settings,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<BeginOidcAuthenticationRequest, string>
    {
        public async Task<string> Handle(BeginOidcAuthenticationRequest command, CancellationToken ct)
        {
            string providerSlug = command.ProviderSlug;
            string? returnUrl = command.ReturnUrl;
            bool trustDevice = command.TrustDevice;
            Guid? linkUserId = command.LinkUserId;
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

        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
        private const string CodeChallengeMethod = "S256";

        private async Task<OidcProvider> GetEnabledProviderAsync(string providerSlug, CancellationToken ct)
        {
            string slug = providerSlug.Trim().ToLowerInvariant();
            OidcProvider provider = await _dbContext.OidcProviders
                .FirstOrDefaultAsync(x => x.Slug == slug, ct)
                ?? throw new EntityNotFoundException<OidcProvider>("Sign-in provider not found.");
            _integrity.RequireValid(_dbContext, provider, "oidc.provider");

            if (!provider.IsEnabled)
            {
                throw new BadRequestException<OidcProvider>("OIDC provider is disabled.");
            }

            return provider;
        }

        private async Task<string> BuildRedirectUriAsync(CancellationToken ct)
        {
            string baseUrl = await _settings.GetPublicBaseUrlAsync(ct);
            return $"{baseUrl}{Routes.V1.Auth}/oidc/callback";
        }

        private Task CleanupExpiredStatesAsync(CancellationToken ct)
        {
            return _dbContext.OidcLoginStates
                .Where(x => x.ExpiresAt < DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
        }
    }
}
