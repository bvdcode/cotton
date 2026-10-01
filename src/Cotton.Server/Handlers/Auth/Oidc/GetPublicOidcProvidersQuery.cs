// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record GetPublicOidcProvidersQuery() : IRequest<IReadOnlyList<PublicOidcProviderDto>>;

    public class GetPublicOidcProvidersQueryHandler(
        CottonDbContext _dbContext)
        : IRequestHandler<GetPublicOidcProvidersQuery, IReadOnlyList<PublicOidcProviderDto>>
    {
        public async Task<IReadOnlyList<PublicOidcProviderDto>> Handle(GetPublicOidcProvidersQuery command, CancellationToken ct)
        {
            List<PublicOidcProviderDto> providers = await _dbContext.OidcProviders
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
    }
}
