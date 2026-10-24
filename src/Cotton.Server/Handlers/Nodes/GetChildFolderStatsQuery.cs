// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Nodes;
using Cotton.Server.Services;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record GetChildFolderStatsQuery(Guid UserId, Guid NodeId, NodeType NodeType, int Depth = 0)
        : IRequest<Dictionary<Guid, FolderStatsDto>>;

    public class GetChildFolderStatsQueryHandler(CottonDbContext database, ILayoutService layouts)
        : IRequestHandler<GetChildFolderStatsQuery, Dictionary<Guid, FolderStatsDto>>
    {
        public async Task<Dictionary<Guid, FolderStatsDto>> Handle(GetChildFolderStatsQuery request, CancellationToken ct)
        {
            ListingRequestLimits.ValidateDepth(request.Depth);
            Layout layout = await layouts.GetOrCreateLatestUserLayoutAsync(request.UserId, ct);
            Node parent = await database.Nodes.AsNoTracking().AccessibleTo(request.UserId)
                .SingleOrDefaultAsync(node => node.Id == request.NodeId
                    && node.LayoutId == layout.Id && node.Type == request.NodeType, ct)
                ?? throw new EntityNotFoundException(nameof(Node), "Folder not found in the requested layout.");
            NodeDirectory directory = new(database, parent);
            List<Guid> parents = await DirectoryListing.ResolveParentIdsAsync(directory, parent.Id, request.Depth, ct);
            IQueryable<Node> children = directory.GetNodes(parents).AsNoTracking();
            if (request.NodeType == NodeType.Trash)
            {
                children = DirectoryListing.HideVersionOnlyTrashWrappers(children, database, request.UserId);
            }
            List<Guid> folderIds = await children.Select(node => node.Id).ToListAsync(ct);
            Dictionary<Guid, FolderStatsDto> totals = folderIds.ToDictionary(id => id, _ => new FolderStatsDto());
            if (folderIds.Count == 0)
            {
                return totals;
            }

            Dictionary<Guid, Guid> branches = folderIds.ToDictionary(id => id);
            List<Guid> frontier = folderIds;
            while (frontier.Count > 0)
            {
                var descendants = await directory.GetNodes(frontier).AsNoTracking()
                    .Select(node => new { node.Id, node.ParentId }).ToListAsync(ct);
                frontier = [];
                foreach (var descendant in descendants)
                {
                    Guid branch = branches[descendant.ParentId!.Value];
                    if (branches.TryAdd(descendant.Id, branch))
                    {
                        FolderStatsDto total = totals[branch];
                        totals[branch] = new FolderStatsDto { Folders = total.Folders + 1 };
                        frontier.Add(descendant.Id);
                    }
                }
            }

            Guid[] ids = [.. branches.Keys];
            var files = await directory.GetFiles(ids).AsNoTracking()
                .Join(database.FileManifests, file => file.FileManifestId, manifest => manifest.Id,
                    (file, manifest) => new { file.NodeId, manifest.SizeBytes })
                .GroupBy(file => file.NodeId)
                .Select(group => new
                {
                    NodeId = group.Key,
                    Files = group.Count(),
                    SizeBytes = group.Sum(file => file.SizeBytes),
                })
                .ToListAsync(ct);
            foreach (var group in files)
            {
                Guid branch = branches[group.NodeId];
                FolderStatsDto total = totals[branch];
                totals[branch] = new FolderStatsDto
                {
                    Folders = total.Folders,
                    Files = total.Files + group.Files,
                    SizeBytes = total.SizeBytes + group.SizeBytes,
                };
            }
            return totals;
        }
    }
}
