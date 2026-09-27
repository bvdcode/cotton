// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    internal record LinkOidcIdentityRequest(Guid UserId, OidcProvider Provider, OidcIdentityClaims Claims) : IRequest<(User User, string? PreviousEmail)>;

    internal class LinkOidcIdentityRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        IMediator _mediator)
        : IRequestHandler<LinkOidcIdentityRequest, (User User, string? PreviousEmail)>
    {
        public async Task<(User User, string? PreviousEmail)> Handle(LinkOidcIdentityRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            OidcProvider provider = command.Provider;
            OidcIdentityClaims claims = command.Claims;
            User user = await _dbContext.Users.FindAsync([userId], ct)
                ?? throw new EntityNotFoundException<User>("Current user not found.");
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
                await _mediator.Send(new SyncOidcUserProfileRequest(user, provider, claims), ct);
                return (user, previousEmail);
            }

            UserExternalIdentity identity = OidcIdentityMapper.Create(user.Id, provider.Id, provider.Issuer, claims);
            await _dbContext.UserExternalIdentities.AddAsync(identity, ct);
            await _mediator.Send(new SyncOidcUserProfileRequest(user, provider, claims), ct);
            return (user, previousEmail);
        }
    }
}
