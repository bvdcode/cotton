// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record ResolveTrashParentsRequest(Guid UserId, IReadOnlyList<TrashParent> Parents, bool CreateMissing)
        : IRequest<TrashRestoreCoordinator.ParentResolution>;

    public class ResolveTrashParentsRequestHandler(CottonDbContext _dbContext, ILayoutNavigator _navigator)
        : IRequestHandler<ResolveTrashParentsRequest, TrashRestoreCoordinator.ParentResolution>
    {
        public async Task<TrashRestoreCoordinator.ParentResolution> Handle(ResolveTrashParentsRequest request, CancellationToken ct)
        {
            var (layout, root) = await _navigator.GetLayoutAndRootAsync(request.UserId, NodeType.Default, ct);
            if (request.Parents.Count == 0)
            {
                return new(null, "Stored parent snapshot is empty.", []);
            }
            Guid[] ids = request.Parents.Select(x => x.Id).ToArray();
            Dictionary<Guid, Node> existing = await _dbContext.Nodes
                .Where(x => ids.Contains(x.Id) && x.OwnerId == request.UserId
                    && x.LayoutId == layout.Id && x.Type == NodeType.Default)
                .ToDictionaryAsync(x => x.Id, ct);
            Node current = root;
            int firstMissing = 1;
            for (int index = request.Parents.Count - 1; index >= 0; index--)
            {
                if (existing.TryGetValue(request.Parents[index].Id, out Node? parent))
                {
                    current = parent;
                    firstMissing = index + 1;
                    break;
                }
            }
            List<Node> created = [];
            foreach (TrashParent part in request.Parents.Skip(firstMissing))
            {
                if (!NameValidator.TryNormalizeAndValidate(part.Name, out string normalized, out string error))
                {
                    return new(null, error, []);
                }
                string nameKey = NameValidator.GetNameKey(normalized);
                Node? child = await _dbContext.Nodes.SingleOrDefaultAsync(x => x.ParentId == current.Id
                    && x.OwnerId == request.UserId && x.LayoutId == layout.Id
                    && x.Type == NodeType.Default && x.NameKey == nameKey, ct);
                if (child is null)
                {
                    if (!request.CreateMissing)
                    {
                        return new(null, null, []);
                    }
                    child = new Node
                    {
                        OwnerId = request.UserId,
                        LayoutId = layout.Id,
                        Type = NodeType.Default,
                        Metadata = TrashParentMetadata.CopyFolderMetadata(part.Metadata),
                    };
                    child.SetParent(current);
                    child.SetName(normalized);
                    _dbContext.Nodes.Add(child);
                    await _dbContext.SaveChangesAsync(ct);
                    created.Add(child);
                }
                current = child;
            }
            return new(current, null, created);
        }
    }
}
