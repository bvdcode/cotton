// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record GetAdminOidcProvidersQuery() : IRequest<IReadOnlyList<OidcProviderDto>>;

    public class GetAdminOidcProvidersQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<GetAdminOidcProvidersQuery, IReadOnlyList<OidcProviderDto>>
    {
        public async Task<IReadOnlyList<OidcProviderDto>> Handle(GetAdminOidcProvidersQuery command, CancellationToken ct)
        {
            List<OidcProvider> providers = await _dbContext.OidcProviders
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            foreach (OidcProvider? provider in providers)
            {
                _integrity.RequireValid(_dbContext, provider, "oidc.admin-list");
            }

            return providers.Select(OidcProviderMapper.ToDto).ToArray();
        }
    }
}
