// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Creates a public share link for a node owned by the requesting user.
    /// </summary>
    public record CreateNodeShareLinkRequest(
        Guid UserId,
        Guid NodeId,
        int ExpireAfterMinutes,
        string? CustomToken) : IRequest<CreateNodeShareLinkResult>;

    /// <summary>
    /// Handles node share link creation.
    /// </summary>
    public class CreateNodeShareLinkRequestHandler(
        CottonDbContext _dbContext,
        PublicShareTokenGenerator _publicShareTokens)
        : IRequestHandler<CreateNodeShareLinkRequest, CreateNodeShareLinkResult>
    {
        private const int MaxExpireMinutes = 60 * 24 * 365;

        /// <summary>
        /// Creates the share token and persists it.
        /// </summary>
        public async Task<CreateNodeShareLinkResult> Handle(
            CreateNodeShareLinkRequest request,
            CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                request.ExpireAfterMinutes,
                MaxExpireMinutes,
                nameof(request.ExpireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                request.ExpireAfterMinutes,
                nameof(request.ExpireAfterMinutes));

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
                ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpireAfterMinutes),
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
