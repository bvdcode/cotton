// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Models.Enums;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public class GetChildrenQuery(
        Guid userId, Guid nodeId, NodeType nodeType,
        int page, int pageSize, int depth = 0, bool includeStats = false,
        DirectoryListingOptions? listing = null) : IRequest<PagedResult<NodeContentDto>>
    {
        public Guid UserId { get; } = userId;

        public Guid NodeId { get; } = nodeId;

        public NodeType NodeType { get; } = nodeType;

        public int Page { get; } = page;

        public int PageSize { get; } = pageSize;

        public int Depth { get; } = depth;

        public bool IncludeStats { get; } = includeStats;
        public DirectoryListingOptions Listing { get; } = listing ?? new();
    }

    public class GetChildrenQueryHandler(
        ILayoutService _layouts,
        CottonDbContext _dbContext)
            : IRequestHandler<GetChildrenQuery, PagedResult<NodeContentDto>>
    {
        public async Task<PagedResult<NodeContentDto>> Handle(GetChildrenQuery request, CancellationToken ct)
        {
            int skip = ListingRequestLimits.GetSkip(request.Page, request.PageSize);
            ListingRequestLimits.ValidateDepth(request.Depth);
            Layout layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId, ct);
            Node parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .AccessibleTo(request.UserId)
                .Where(x => x.Id == request.NodeId
                    && x.LayoutId == layout.Id
                    && x.Type == request.NodeType)
                .SingleOrDefaultAsync(cancellationToken: ct)
                    ?? throw new EntityNotFoundException(nameof(Node), "Folder not found in the requested layout.");
            NodeDirectory directory = new(_dbContext, parentNode);
            List<Guid> currentParentIds = await DirectoryListing.ResolveParentIdsAsync(
                directory, parentNode.Id, request.Depth, ct);
            if (currentParentIds.Count == 0)
            {
                return new(new NodeContentDto
                {
                    Nodes = [],
                    Files = [],
                    Id = request.NodeId,
                    CreatedAt = parentNode.CreatedAt,
                    UpdatedAt = parentNode.UpdatedAt,
                    Stats = request.IncludeStats ? new FolderStatsDto() : null,
                }, 0);
            }

            IQueryable<Node> nodesBaseQuery = directory.GetNodes(currentParentIds).AsNoTracking();
            IQueryable<NodeFile> filesBaseQuery = directory.GetFiles(currentParentIds).AsNoTracking();

            if (request.NodeType == NodeType.Trash)
            {
                nodesBaseQuery = DirectoryListing.HideVersionOnlyTrashWrappers(nodesBaseQuery, _dbContext, request.UserId);
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

            var (nodes, files, nodeCount, fileCount) = await DirectoryListing.ReadPageAsync<NodeFileManifestDto>(
                nodesBaseQuery, filesBaseQuery, skip, request.PageSize, ct, request.Listing);

            FolderStatsDto? stats = null;
            if (request.IncludeStats)
            {
                stats = await LoadStatsAsync(nodesBaseQuery, filesBaseQuery, nodeCount, fileCount,
                    request.Listing.FilterOperator != DirectoryFilterOperator.None, ct);
            }

            return new(new NodeContentDto
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt,
                Stats = stats,
            }, nodeCount + fileCount);
        }

        private static async Task<FolderStatsDto> LoadStatsAsync(
            IQueryable<Node> nodes,
            IQueryable<NodeFile> files,
            int nodeCount,
            int fileCount,
            bool filtered,
            CancellationToken ct)
        {
            long sizeBytes = await files.SumAsync(file => (long?)file.FileManifest.SizeBytes, ct) ?? 0L;
            return new FolderStatsDto
            {
                Folders = filtered ? await nodes.CountAsync(ct) : nodeCount,
                Files = filtered ? await files.CountAsync(ct) : fileCount,
                SizeBytes = sizeBytes,
            };
        }

        private async Task<PagedResult<NodeContentDto>> LoadTrashChildrenAsync(
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

            return new(new NodeContentDto
            {
                Nodes = nodes,
                Files = files,
                Id = request.NodeId,
                CreatedAt = parentNode.CreatedAt,
                UpdatedAt = parentNode.UpdatedAt,
            }, nodesCount + filesCount);
        }

        private class TrashChildPageEntry
        {
            public Guid Id { get; init; }

            public bool IsNode { get; init; }

            public DateTime UpdatedAt { get; init; }

            public string NameKey { get; init; } = string.Empty;
        }

    }
}
