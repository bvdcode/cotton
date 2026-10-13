// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using Cotton.Topology;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record CaptureBatchTrashParentsQuery(Guid UserId, IReadOnlyCollection<Guid> ParentIds)
        : IRequest<IReadOnlyDictionary<Guid, IReadOnlyList<TrashParent>>>;

    public class CaptureBatchTrashParentsQueryHandler(CottonDbContext _db)
        : IRequestHandler<CaptureBatchTrashParentsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<TrashParent>>>
    {
        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TrashParent>>> Handle(
            CaptureBatchTrashParentsQuery request, CancellationToken ct)
        {
            IQueryable<Node> query = _db.Nodes.AsNoTracking()
                .Where(x => x.OwnerId == request.UserId && x.Type == NodeType.Default);
            ResolvedNodePaths paths = await NodeHierarchy.ResolvePathsWithAncestorsAsync(query, request.ParentIds, ct);
            Dictionary<Guid, Node> ancestors = await query.Where(x => paths.AncestorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            Dictionary<Guid, IReadOnlyList<TrashParent>> result = [];
            foreach (Guid parentId in request.ParentIds.Distinct())
            {
                List<TrashParent> parents = [];
                HashSet<Guid> visited = [];
                Guid? current = parentId;
                while (current.HasValue && ancestors.TryGetValue(current.Value, out Node? node))
                {
                    if (!visited.Add(node.Id) || parents.Count > NodeHierarchy.DefaultMaxDepth)
                    {
                        throw new NodeHierarchyException("Invalid node hierarchy while capturing trash parents.");
                    }
                    parents.Add(new TrashParent(node.Id, node.Name, TrashParentMetadata.CopyFolderMetadata(node.Metadata)));
                    current = node.ParentId;
                }
                parents.Reverse();
                result.Add(parentId, parents);
            }
            return result;
        }
    }
}
