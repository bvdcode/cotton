// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public record CreateNodeShareLinkRequest(
        Guid UserId,
        Guid NodeId,
        int? ExpireAfterMinutes,
        string? CustomToken) : IRequest<CreateNodeShareLinkResult>;

    public class CreateNodeShareLinkRequestHandler(
        CottonDbContext _dbContext,
        PublicShareTokenGenerator _publicShareTokens)
        : IRequestHandler<CreateNodeShareLinkRequest, CreateNodeShareLinkResult>
    {
        public async Task<CreateNodeShareLinkResult> Handle(
            CreateNodeShareLinkRequest request,
            CancellationToken ct)
        {
            DateTime? expiresAt = ShareLinkExpiration.FromMinutes(request.ExpireAfterMinutes);

            Node? node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (node is null)
            {
                return new CreateNodeShareLinkResult(
                    CreateNodeShareLinkStatus.NodeNotFound);
            }

            string token;
            if (!string.IsNullOrWhiteSpace(request.CustomToken))
            {
                bool exists = await _dbContext.DownloadTokens
                    .AnyAsync(x => x.Token == request.CustomToken, ct)
                    || await _dbContext.NodeShareTokens
                        .AnyAsync(x => x.Token == request.CustomToken, ct);
                if (exists)
                {
                    return new CreateNodeShareLinkResult(
                        CreateNodeShareLinkStatus.TokenConflict);
                }

                token = request.CustomToken;
            }
            else
            {
                token = await _publicShareTokens.CreateUniqueAsync(ct);
            }

            NodeShareToken shareToken = new()
            {
                Name = node.Name,
                NodeId = node.Id,
                CreatedByUserId = request.UserId,
                ExpiresAt = expiresAt,
                Token = token,
            };

            await _dbContext.NodeShareTokens.AddAsync(shareToken, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new CreateNodeShareLinkResult(
                CreateNodeShareLinkStatus.Created,
                $"/s/{shareToken.Token}");
        }
    }
}
