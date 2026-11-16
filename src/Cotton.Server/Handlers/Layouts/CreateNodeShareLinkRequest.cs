// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public record CreateNodeShareLinkRequest(
        Guid UserId,
        Guid NodeId,
        int? ExpireAfterMinutes,
        string? CustomToken,
        bool AllowUpload = false,
        bool HideFolderContent = false) : IRequest<CreateNodeShareLinkResult>;

    public class CreateNodeShareLinkRequestHandler(
        CottonDbContext _dbContext,
        PublicShareTokenGenerator _publicShareTokens)
        : IRequestHandler<CreateNodeShareLinkRequest, CreateNodeShareLinkResult>
    {
        public async Task<CreateNodeShareLinkResult> Handle(
            CreateNodeShareLinkRequest request,
            CancellationToken ct)
        {
            if (request.HideFolderContent && !request.AllowUpload)
            {
                throw new BadRequestException("Hiding folder content requires file uploads to be enabled.");
            }
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

            NodeShareToken shareToken = new()
            {
                Name = node.Name,
                NodeId = node.Id,
                CreatedByUserId = request.UserId,
                ExpiresAt = expiresAt,
                Token = !string.IsNullOrWhiteSpace(request.CustomToken)
                    ? request.CustomToken
                    : await _publicShareTokens.CreateUniqueAsync(ct),
                AllowUpload = request.AllowUpload,
                HideFolderContent = request.HideFolderContent,
            };

            bool created = await _dbContext.TryCreatePublicShareTokenAsync(shareToken, ct);
            if (!created)
            {
                return new CreateNodeShareLinkResult(CreateNodeShareLinkStatus.TokenConflict);
            }
            return new CreateNodeShareLinkResult(
                CreateNodeShareLinkStatus.Created,
                $"/s/{shareToken.Token}");
        }
    }
}
