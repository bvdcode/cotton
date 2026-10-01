// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    public record DeleteOidcProviderRequest(Guid ProviderId) : IRequest;

    public class DeleteOidcProviderRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<DeleteOidcProviderRequest>
    {
        public async Task Handle(DeleteOidcProviderRequest command, CancellationToken ct)
        {
            Guid providerId = command.ProviderId;
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(ct);
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], ct)
                ?? throw new EntityNotFoundException<OidcProvider>("Sign-in provider not found.");
            _integrity.RequireValid(_dbContext, provider, "oidc.admin-delete");

            await _dbContext.OidcLoginStates
                .Where(state => state.ProviderId == providerId)
                .ExecuteDeleteAsync(ct);
            await _dbContext.UserExternalIdentities
                .Where(identity => identity.ProviderId == providerId)
                .ExecuteDeleteAsync(ct);

            _dbContext.OidcProviders.Remove(provider);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
