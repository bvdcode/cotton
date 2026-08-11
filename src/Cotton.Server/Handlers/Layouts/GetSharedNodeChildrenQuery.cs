// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Models.Dto;
using Cotton.Storage.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Loads one page of nodes and files inside a shared folder.
    /// </summary>
    public record GetSharedNodeChildrenQuery(
        string Token,
        Guid? NodeId,
        int Page,
        int PageSize) : IRequest<GetSharedNodeChildrenResult>;

    /// <summary>
    /// Handles shared folder content queries.
    /// </summary>
    public class GetSharedNodeChildrenQueryHandler(
        IMediator _mediator,
        CottonDbContext _dbContext)
        : IRequestHandler<GetSharedNodeChildrenQuery, GetSharedNodeChildrenResult>
    {
        /// <summary>
        /// Loads the requested shared folder page.
        /// </summary>
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
            bool canAccessNode = await _mediator.Send(
                new VerifySharedNodeSubtreeAccessQuery(
                    targetNodeId,
                    access.NodeId,
                    access.CreatedByUserId),
                ct);
            if (!canAccessNode)
            {
                return new GetSharedNodeChildrenResult(
                    GetSharedNodeChildrenStatus.FolderNotFound);
            }

            Node? targetNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == targetNodeId
                    && x.OwnerId == access.CreatedByUserId
                    && x.Type == NodeType.Default)
                .SingleOrDefaultAsync(ct);
            if (targetNode is null)
            {
                return new GetSharedNodeChildrenResult(
                    GetSharedNodeChildrenStatus.FolderNotFound);
            }

            int skip = (request.Page - 1) * request.PageSize;
            IQueryable<NodeDto> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.ParentId == targetNodeId
                    && x.OwnerId == access.CreatedByUserId
                    && x.Type == NodeType.Default)
                .ProjectToType<NodeDto>();
            IQueryable<NodeFile> filesQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.NodeId == targetNodeId
                    && x.OwnerId == access.CreatedByUserId);

            int nodesCount = await nodesQuery.CountAsync(ct);
            int filesCount = await filesQuery.CountAsync(ct);
            int nodesToTake = Math.Max(
                0,
                Math.Min(request.PageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, request.PageSize - nodesToTake);

            List<NodeDto> nodes = nodesToTake == 0
                ? []
                : await nodesQuery
                    .Skip(skip)
                    .Take(nodesToTake)
                    .ToListAsync(ct);
            List<SharedNodeFileDto> files = filesToTake == 0
                ? []
                : await LoadSharedFilesAsync(
                    filesQuery,
                    filesSkip,
                    filesToTake,
                    ct);

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
                nodesCount + filesCount);
        }

        private static async Task<List<SharedNodeFileDto>> LoadSharedFilesAsync(
            IQueryable<NodeFile> filesQuery,
            int skip,
            int take,
            CancellationToken ct)
        {
            List<NodeFile> files = await filesQuery
                .OrderBy(x => x.NameKey)
                .Include(x => x.FileManifest)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            return [.. files.Select(x => new SharedNodeFileDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                NodeId = x.NodeId,
                Name = x.Name,
                ContentType = x.FileManifest.ContentType,
                SizeBytes = x.FileManifest.SizeBytes,
                PreviewHashEncryptedHex = x.FileManifest.GetPreviewHashEncryptedHex(),
            })];
        }
    }
}
