// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record GetLinkedOidcIdentitiesQuery(Guid UserId) : IRequest<IReadOnlyList<UserExternalIdentityDto>>;

    public class GetLinkedOidcIdentitiesQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<GetLinkedOidcIdentitiesQuery, IReadOnlyList<UserExternalIdentityDto>>
    {
        public async Task<IReadOnlyList<UserExternalIdentityDto>> Handle(GetLinkedOidcIdentitiesQuery command, CancellationToken ct)
        {
            Guid userId = command.UserId;
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
    }
}
