// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Auth
{
    public record InvalidateShareLinksRequest(Guid UserId) : IRequest;

    public class InvalidateShareLinksRequestHandler(
        CottonDbContext _dbContext) : IRequestHandler<InvalidateShareLinksRequest>
    {
        private const int BatchSize = 1_000;

        public async Task Handle(InvalidateShareLinksRequest request, CancellationToken cancellationToken)
        {
            DateTime expiresAt = DateTime.UtcNow;
            while (true)
            {
                List<DownloadToken> fileTokens = await _dbContext.DownloadTokens
                    .Where(x => x.CreatedByUserId == request.UserId
                        && (x.ExpiresAt == null || x.ExpiresAt > expiresAt))
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                List<NodeShareToken> folderTokens = await _dbContext.NodeShareTokens
                    .Where(x => x.CreatedByUserId == request.UserId
                        && (x.ExpiresAt == null || x.ExpiresAt > expiresAt))
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (fileTokens.Count == 0 && folderTokens.Count == 0)
                {
                    return;
                }

                foreach (DownloadToken token in fileTokens)
                {
                    token.ExpiresAt = expiresAt;
                }
                foreach (NodeShareToken token in folderTokens)
                {
                    token.ExpiresAt = expiresAt;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}
