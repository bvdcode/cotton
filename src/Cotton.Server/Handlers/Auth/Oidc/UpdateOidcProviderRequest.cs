// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record UpdateOidcProviderRequest(Guid ProviderId, OidcProviderRequestDto Provider) : IRequest<OidcProviderDto>;

    public class UpdateOidcProviderRequestHandler(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        IDatabaseIntegrityVerifier _integrity,
        IMediator _mediator)
        : IRequestHandler<UpdateOidcProviderRequest, OidcProviderDto>
    {
        public async Task<OidcProviderDto> Handle(UpdateOidcProviderRequest command, CancellationToken ct)
        {
            Guid providerId = command.ProviderId;
            OidcProviderRequestDto request = command.Provider;
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], ct)
                ?? throw new EntityNotFoundException<OidcProvider>("Sign-in provider not found.");
            _integrity.RequireValid(_dbContext, provider, "oidc.admin-update");

            OidcProviderInput input = OidcProviderInput.Normalize(request);
            provider.Name = input.Name;
            provider.Slug = await _mediator.Send(new ResolveOidcProviderSlugQuery(input.Slug, input.Name, provider.Id), ct);
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

            if (provider.IsEnabled)
            {
                await _discovery.ValidateConfigurationAsync(provider, ct);
            }
            await _dbContext.SaveChangesAsync(ct);
            return OidcProviderMapper.ToDto(provider);
        }
    }
}
