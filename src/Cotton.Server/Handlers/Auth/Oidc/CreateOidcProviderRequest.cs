// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record CreateOidcProviderRequest(OidcProviderRequestDto Provider) : IRequest<OidcProviderDto>;

    public class CreateOidcProviderRequestHandler(
        CottonDbContext _dbContext,
        OidcDiscoveryService _discovery,
        IMediator _mediator)
        : IRequestHandler<CreateOidcProviderRequest, OidcProviderDto>
    {
        public async Task<OidcProviderDto> Handle(CreateOidcProviderRequest command, CancellationToken ct)
        {
            OidcProviderRequestDto request = command.Provider;
            OidcProviderInput input = OidcProviderInput.Normalize(request);
            string slug = await _mediator.Send(new ResolveOidcProviderSlugQuery(input.Slug, input.Name), ct);

            OidcProvider provider = new OidcProvider
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

            if (provider.IsEnabled)
            {
                await _discovery.ValidateConfigurationAsync(provider, ct);
            }
            await _dbContext.OidcProviders.AddAsync(provider, ct);
            await _dbContext.SaveChangesAsync(ct);
            return OidcProviderMapper.ToDto(provider);
        }
    }
}
