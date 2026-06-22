// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Represents a get children query sent through the mediator pipeline.
    /// </summary>
    public class GetChildrenQuery(
        Guid userId, Guid nodeId, NodeType nodeType,
        int page, int pageSize, int depth = 0) : IRequest<NodeContentDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the node identifier.
        /// </summary>
        public Guid NodeId { get; } = nodeId;
        /// <summary>
        /// Gets the node type.
        /// </summary>
        public NodeType NodeType { get; } = nodeType;
        /// <summary>
        /// Gets the page.
        /// </summary>
        public int Page { get; } = page;
        /// <summary>
        /// Gets the page size.
        /// </summary>
        public int PageSize { get; } = pageSize;
        /// <summary>
        /// Gets the depth.
        /// </summary>
        public int Depth { get; } = depth;
    }

    /// <summary>
    /// Handles get children queries in the mediator pipeline.
    /// </summary>
    public class GetChildrenQueryHandler(
        ILayoutService _layouts,
        CottonDbContext _dbContext)
            : IRequestHandler<GetChildrenQuery, NodeContentDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<NodeContentDto> Handle(GetChildrenQuery request, CancellationToken ct)
        {
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId, ct);
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.Id == request.NodeId
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PageSize);
            ArgumentOutOfRangeException.ThrowIfNegative(request.Depth);

            // Resolve the set of parent IDs whose children should be returned.
            // depth == 0: direct children of parentNode (default).
            // depth == N: skip N intermediate levels and return their descendants.
            // We materialize each level into a list to avoid deeply nested IQueryable<T>
            // expression trees that cause EF Core's ExpressionTreeFuncletizer to stack-overflow.
            List<Guid> currentParentIds = [parentNode.Id];

            for (int i = 0; i < request.Depth; i++)
            {
                currentParentIds = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.ParentId != null
                        && currentParentIds.Contains(x.ParentId.Value)
                        && x.OwnerId == request.UserId
                        && x.LayoutId == layout.Id
                        && x.Type == request.NodeType)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken: ct);

                if (currentParentIds.Count == 0)
                {
                    return new()
                    {
                        Nodes = [],
                        Files = [],
                        Id = request.NodeId,
                        CreatedAt = parentNode.CreatedAt,
                        UpdatedAt = parentNode.UpdatedAt,
                        TotalCount = 0,
                    };
                }
            }

            int skip = (request.Page - 1) * request.PageSize;
            IQueryable<Node> nodesBaseQuery = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.ParentId != null
                    && currentParentIds.Contains(x.ParentId.Value)
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType);

            IQueryable<NodeFile> filesBaseQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => currentParentIds.Contains(x.NodeId)
                    && x.OwnerId == request.UserId);

            if (request.NodeType == NodeType.Trash)
            {
                nodesBaseQuery = HideVersionOnlyTrashWrappers(nodesBaseQuery, request.UserId);
                filesBaseQuery = filesBaseQuery
                    .Where(x => x.OriginalNodeFileId == Guid.Empty || x.Id == x.OriginalNodeFileId);

                return await LoadTrashChildrenAsync(
                    request,
                    parentNode,
                    nodesBaseQuery,
                    filesBaseQuery,
                    skip,
                    ct);
            }

            IQueryable<NodeDto> nodesQuery = nodesBaseQuery
                .OrderBy(x => x.NameKey)
                .ProjectToType<NodeDto>();

            int nodesCount = await nodesQuery.CountAsync(cancellationToken: ct);
            int filesCount = await filesBaseQuery.CountAsync(cancellationToken: ct);

            var nodesToTake = Math.Max(0, Math.Min(request.PageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, request.PageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync(cancellationToken: ct);

            var files = filesToTake == 0 ? []
                : await filesBaseQuery
                    .OrderBy(x => x.NameKey)
                    .Include(x => x.FileManifest)
                    .Skip(filesSkip)
                    .Take(filesToTake)
                    .ProjectToType<NodeFileManifestDto>()
                    .ToListAsync(cancellationToken: ct);

            return new()
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt,
                TotalCount = nodesCount + filesCount,
            };
        }

        private async Task<NodeContentDto> LoadTrashChildrenAsync(
            GetChildrenQuery request,
            Node parentNode,
            IQueryable<Node> nodesBaseQuery,
            IQueryable<NodeFile> filesBaseQuery,
            int skip,
            CancellationToken ct)
        {
            int nodesCount = await nodesBaseQuery.CountAsync(cancellationToken: ct);
            int filesCount = await filesBaseQuery.CountAsync(cancellationToken: ct);

            IQueryable<TrashChildPageEntry> nodeEntriesQuery = nodesBaseQuery
                .Select(x => new TrashChildPageEntry
                {
                    Id = x.Id,
                    IsNode = true,
                    UpdatedAt = x.UpdatedAt,
                    NameKey = x.NameKey,
                });
            IQueryable<TrashChildPageEntry> fileEntriesQuery = filesBaseQuery
                .Select(x => new TrashChildPageEntry
                {
                    Id = x.Id,
                    IsNode = false,
                    UpdatedAt = x.UpdatedAt,
                    NameKey = x.NameKey,
                });

            List<TrashChildPageEntry> pageEntries = await nodeEntriesQuery
                .Concat(fileEntriesQuery)
                .OrderByDescending(x => x.UpdatedAt)
                .ThenBy(x => x.NameKey)
                .ThenBy(x => x.Id)
                .Skip(skip)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken: ct);

            Guid[] nodeIds = [.. pageEntries.Where(x => x.IsNode).Select(x => x.Id)];
            Guid[] fileIds = [.. pageEntries.Where(x => !x.IsNode).Select(x => x.Id)];

            List<NodeDto> nodes = nodeIds.Length == 0 ? []
                : await nodesBaseQuery
                    .Where(x => nodeIds.Contains(x.Id))
                    .ProjectToType<NodeDto>()
                    .ToListAsync(cancellationToken: ct);

            List<NodeFileManifestDto> files = fileIds.Length == 0 ? []
                : await filesBaseQuery
                    .Where(x => fileIds.Contains(x.Id))
                    .Include(x => x.FileManifest)
                    .ProjectToType<NodeFileManifestDto>()
                    .ToListAsync(cancellationToken: ct);

            Dictionary<Guid, int> entryOrder = pageEntries
                .Select((entry, index) => new { entry.Id, Index = index })
                .ToDictionary(x => x.Id, x => x.Index);
            nodes.Sort((a, b) => entryOrder[a.Id].CompareTo(entryOrder[b.Id]));
            files.Sort((a, b) => entryOrder[a.Id].CompareTo(entryOrder[b.Id]));

            return new()
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt,
                TotalCount = nodesCount + filesCount,
            };
        }

        private sealed class TrashChildPageEntry
        {
            public Guid Id { get; init; }

            public bool IsNode { get; init; }

            public DateTime UpdatedAt { get; init; }

            public string NameKey { get; init; } = string.Empty;
        }

        private IQueryable<Node> HideVersionOnlyTrashWrappers(IQueryable<Node> nodesQuery, Guid userId)
        {
            return nodesQuery.Where(node =>
                _dbContext.Nodes.Any(child => child.ParentId == node.Id && child.OwnerId == userId)
                || _dbContext.NodeFiles.Any(file => file.NodeId == node.Id
                    && file.OwnerId == userId
                    && (file.OriginalNodeFileId == Guid.Empty || file.Id == file.OriginalNodeFileId))
                || !_dbContext.NodeFiles.Any(file => file.NodeId == node.Id
                    && file.OwnerId == userId
                    && file.OriginalNodeFileId != Guid.Empty
                    && file.Id != file.OriginalNodeFileId));
        }
    }
}
