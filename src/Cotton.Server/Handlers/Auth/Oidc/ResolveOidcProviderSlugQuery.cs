// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record ResolveOidcProviderSlugQuery(string? RequestedSlug, string Name, Guid? CurrentProviderId = null) : IRequest<string>;

    public class ResolveOidcProviderSlugQueryHandler(
        CottonDbContext _dbContext)
        : IRequestHandler<ResolveOidcProviderSlugQuery, string>
    {
        public async Task<string> Handle(ResolveOidcProviderSlugQuery command, CancellationToken ct)
        {
            string? requestedSlug = command.RequestedSlug;
            string name = command.Name;
            Guid? currentProviderId = command.CurrentProviderId;
            string slug = string.IsNullOrWhiteSpace(requestedSlug)
                ? OidcProviderInput.Slugify(name)
                : OidcProviderInput.NormalizeSlug(requestedSlug);

            bool exists = await _dbContext.OidcProviders.AnyAsync(
                x => x.Slug == slug && x.Id != currentProviderId,
                ct);
            if (exists)
            {
                throw new BadRequestException<OidcProvider>("OIDC provider slug is already used.");
            }

            return slug;
        }
    }
}
