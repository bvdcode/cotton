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

namespace Cotton.Server.Handlers.Files
{
    public record CreateFileShareLinkRequest(
        Guid UserId,
        Guid NodeFileId,
        int? ExpireAfterMinutes,
        string? CustomToken,
        bool DeleteAfterUse) : IRequest<CreateFileShareLinkResult>;

    public class CreateFileShareLinkRequestHandler(
        CottonDbContext _dbContext,
        PublicShareTokenGenerator _publicShareTokens)
        : IRequestHandler<CreateFileShareLinkRequest, CreateFileShareLinkResult>
    {
        public async Task<CreateFileShareLinkResult> Handle(
            CreateFileShareLinkRequest request,
            CancellationToken ct)
        {
            DateTime? expiresAt = ShareLinkExpiration.FromMinutes(request.ExpireAfterMinutes);
            if (!string.IsNullOrWhiteSpace(request.CustomToken))
            {
                bool exists = await _dbContext.DownloadTokens
                    .AnyAsync(x => x.Token == request.CustomToken, ct)
                    || await _dbContext.NodeShareTokens
                        .AnyAsync(x => x.Token == request.CustomToken, ct);
                if (exists)
                {
                    return new CreateFileShareLinkResult(CreateFileShareLinkStatus.TokenConflict);
                }
            }

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Where(x => x.Id == request.NodeFileId && x.OwnerId == request.UserId)
                .Include(x => x.Node)
                .SingleOrDefaultAsync(ct);
            if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
            {
                return new CreateFileShareLinkResult(CreateFileShareLinkStatus.FileNotFound);
            }

            DownloadToken token = new()
            {
                FileName = nodeFile.Name,
                DeleteAfterUse = request.DeleteAfterUse,
                CreatedByUserId = request.UserId,
                NodeFileId = nodeFile.Id,
                ExpiresAt = expiresAt,
                Token = !string.IsNullOrWhiteSpace(request.CustomToken)
                    ? request.CustomToken
                    : await _publicShareTokens.CreateUniqueAsync(ct),
            };
            await _dbContext.DownloadTokens.AddAsync(token, ct);
            await _dbContext.SaveChangesAsync(ct);
            return new CreateFileShareLinkResult(
                CreateFileShareLinkStatus.Created,
                Routes.V1.Files + $"/{nodeFile.Id}/download?token={token.Token}");
        }
    }
}
