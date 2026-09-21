// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Topology;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeChildrenQuery(
        string Token,
        Guid? NodeId,
        int Page,
        int PageSize) : IRequest<GetSharedNodeChildrenResult>;

    public class GetSharedNodeChildrenQueryHandler(
        IMediator _mediator,
        CottonDbContext _dbContext)
        : IRequestHandler<GetSharedNodeChildrenQuery, GetSharedNodeChildrenResult>
    {
        public async Task<GetSharedNodeChildrenResult> Handle(
            GetSharedNodeChildrenQuery request,
            CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PageSize);

            SharedNodeAccess? access = await _mediator.Send(
                new ResolveSharedNodeAccessQuery(request.Token),
                ct);
            if (access is null)
            {
                return new GetSharedNodeChildrenResult(
                    GetSharedNodeChildrenStatus.SharedFolderNotFound);
            }

            Guid targetNodeId = request.NodeId ?? access.NodeId;
            IReadOnlyList<Node>? ancestry = await _mediator.Send(
                new ResolveSharedNodeAncestryQuery(targetNodeId, access.NodeId, access.CreatedByUserId), ct);
            if (ancestry is null)
            {
                return new GetSharedNodeChildrenResult(GetSharedNodeChildrenStatus.FolderNotFound);
            }
            Node targetNode = ancestry[0];
            NodeDirectory directory = new(_dbContext, targetNode);
            int skip = (request.Page - 1) * request.PageSize;
            var (nodes, files, totalCount) = await DirectoryListing.ReadPageAsync<SharedNodeFileDto>(
                directory.Nodes.AsNoTracking(), directory.Files.AsNoTracking(), skip, request.PageSize, ct);

            SharedNodeContentDto content = new()
            {
                Nodes = nodes,
                Files = files,
                Id = targetNode.Id,
                CreatedAt = targetNode.CreatedAt,
                UpdatedAt = targetNode.UpdatedAt,
            };
            return new GetSharedNodeChildrenResult(
                GetSharedNodeChildrenStatus.Success,
                content,
                totalCount);
        }
    }
}
