// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Resolves and verifies an active shared node boundary.
    /// </summary>
    public record ResolveSharedNodeAccessQuery(string Token)
        : IRequest<SharedNodeAccess?>;

    /// <summary>
    /// Handles shared node boundary resolution.
    /// </summary>
    public class ResolveSharedNodeAccessQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<ResolveSharedNodeAccessQuery, SharedNodeAccess?>
    {
        /// <summary>
        /// Resolves an active token and verifies its signed database graph.
        /// </summary>
        public async Task<SharedNodeAccess?> Handle(
            ResolveSharedNodeAccessQuery request,
            CancellationToken ct)
        {
            DateTime now = DateTime.UtcNow;
            NodeShareToken? shareToken = await _dbContext.NodeShareTokens
                .Include(x => x.Node)
                .Where(x => x.Token == request.Token
                    && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .SingleOrDefaultAsync(ct);
            if (shareToken is null)
            {
                return null;
            }

            _integrity.RequireValid(
                _dbContext,
                shareToken,
                "shared-folder.node-token");
            _integrity.RequireValid(
                _dbContext,
                shareToken.Node,
                "shared-folder.root-node");
            if (shareToken.Node.Type != NodeType.Default)
            {
                return null;
            }

            return new SharedNodeAccess(
                shareToken.Token,
                shareToken.NodeId,
                shareToken.CreatedByUserId,
                shareToken.Name,
                shareToken.ExpiresAt);
        }
    }
}
