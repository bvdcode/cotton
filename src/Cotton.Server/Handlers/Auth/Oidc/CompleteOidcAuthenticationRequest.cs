// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record CompleteOidcAuthenticationRequest(string State, string Code) : IRequest<string>;

    public class CompleteOidcAuthenticationRequestHandler(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        SettingsProvider _settings,
        IDatabaseIntegrityVerifier _integrity,
        IMediator _mediator,
        INotificationsProvider _notifications,
        AuthSessionIssuer _sessionIssuer,
        ILogger<CompleteOidcAuthenticationRequestHandler> _logger)
        : IRequestHandler<CompleteOidcAuthenticationRequest, string>
    {
        public async Task<string> Handle(CompleteOidcAuthenticationRequest command, CancellationToken ct)
        {
            string state = command.State;
            string code = command.Code;
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
                (user, securityEmailRecipient) = await _mediator.Send(new LinkOidcIdentityRequest(linkUserId, loginState.Provider, claims), ct);
            }
            else
            {
                user = await _mediator.Send(new ResolveOidcUserRequest(loginState.Provider, claims), ct);
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

        private async Task<string> BuildRedirectUriAsync(CancellationToken ct)
        {
            string baseUrl = await _settings.GetPublicBaseUrlAsync(ct);
            return $"{baseUrl}{Routes.V1.Auth}/oidc/callback";
        }
    }
}
