// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record LoadDeletionBatchQuery(Guid UserId, IReadOnlyList<BatchItemRequestDto> Items, bool SkipTrash)
        : IRequest<DeletionBatch>;

    public class LoadDeletionBatchQueryHandler(CottonDbContext _db, NodeSubtreeService _subtree)
        : IRequestHandler<LoadDeletionBatchQuery, DeletionBatch>
    {
        public async Task<DeletionBatch> Handle(LoadDeletionBatchQuery request, CancellationToken ct)
        {
            Guid[] folderIds = [.. request.Items.Where(x => x.Kind == BatchItemKind.Folder).Select(x => x.Id).Distinct()];
            Guid[] fileIds = [.. request.Items.Where(x => x.Kind == BatchItemKind.File).Select(x => x.Id).Distinct()];
            Dictionary<Guid, Node> selectedFolders = await _db.Nodes
                .Where(x => x.OwnerId == request.UserId && folderIds.Contains(x.Id)
                    && x.ParentId != null && (request.SkipTrash || x.Type == NodeType.Default))
                .ToDictionaryAsync(x => x.Id, ct);
            HashSet<Guid> subtreeIds = await _subtree.CollectSubtreeIdsAsync(request.UserId, selectedFolders.Keys.ToArray(), ct);
            Dictionary<Guid, Node> tree = await _db.Nodes
                .Where(x => x.OwnerId == request.UserId && subtreeIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            if (request.SkipTrash)
            {
                Guid[] versionNodes = await _db.NodeFiles.AsNoTracking()
                    .Where(x => x.OwnerId == request.UserId && subtreeIds.Contains(x.NodeId)
                        && x.OriginalNodeFileId != Guid.Empty && x.Id != x.OriginalNodeFileId)
                    .Select(x => x.NodeId).Distinct().ToArrayAsync(ct);
                foreach (Guid nodeId in versionNodes)
                {
                    foreach (Guid ancestorId in Ancestors(tree, nodeId))
                    {
                        selectedFolders.Remove(ancestorId);
                    }
                }
                subtreeIds = Descendants(tree.Values, selectedFolders.Keys);
            }

            List<NodeFile> files = await _db.NodeFiles.Include(x => x.Node).Include(x => x.FileManifest)
                .Where(x => x.OwnerId == request.UserId
                    && (fileIds.Contains(x.Id) || (request.SkipTrash && subtreeIds.Contains(x.NodeId)))
                    && (request.SkipTrash || x.Node.Type == NodeType.Default))
                .ToListAsync(ct);
            HashSet<Guid> validFileIds = [.. files.Where(x => fileIds.Contains(x.Id)).Select(x => x.Id)];

            if (request.SkipTrash)
            {
                Guid[] lineages = [.. files.Select(FileVersionService.GetLineageId).Distinct()];
                List<NodeFile> versions = await _db.NodeFiles.Include(x => x.Node).Include(x => x.FileManifest)
                    .Where(x => x.OwnerId == request.UserId && lineages.Contains(x.OriginalNodeFileId)
                        && x.Id != x.OriginalNodeFileId)
                    .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
                HashSet<Guid> removedLineages = [.. files.Where(x => !FileVersionService.IsHistoricalVersion(x))
                    .Select(FileVersionService.GetLineageId)];
                foreach (IGrouping<Guid, NodeFile> lineage in versions.GroupBy(x => x.OriginalNodeFileId))
                {
                    if (removedLineages.Contains(lineage.Key))
                    {
                        continue;
                    }
                    NodeFile original = lineage.First();
                    validFileIds.Remove(original.Id);
                    files.RemoveAll(x => x.Id == original.Id);
                }
                files = files.Concat(versions.Where(x => removedLineages.Contains(x.OriginalNodeFileId)))
                    .DistinctBy(x => x.Id).ToList();
            }
            else
            {
                files.RemoveAll(x => subtreeIds.Contains(x.NodeId));
            }

            List<Node> roots = selectedFolders.Values.Where(x => !Ancestors(tree, x.ParentId)
                .Any(selectedFolders.ContainsKey)).ToList();
            return new DeletionBatch(roots, files, tree.Values.Where(x => subtreeIds.Contains(x.Id)).ToList(),
                selectedFolders.Keys.ToHashSet(), validFileIds);
        }

        private static IEnumerable<Guid> Ancestors(IReadOnlyDictionary<Guid, Node> tree, Guid? id)
        {
            HashSet<Guid> visited = [];
            while (id.HasValue && tree.TryGetValue(id.Value, out Node? node))
            {
                if (!visited.Add(node.Id))
                {
                    throw new InvalidOperationException("Circular node hierarchy.");
                }
                yield return node.Id;
                id = node.ParentId;
            }
        }

        private static HashSet<Guid> Descendants(IEnumerable<Node> tree, IEnumerable<Guid> roots)
        {
            ILookup<Guid?, Node> children = tree.ToLookup(x => x.ParentId);
            HashSet<Guid> ids = new(roots);
            Queue<Guid> pending = new(ids);
            while (pending.TryDequeue(out Guid parentId))
            {
                foreach (Node child in children[parentId])
                {
                    if (ids.Add(child.Id))
                    {
                        pending.Enqueue(child.Id);
                    }
                }
            }
            return ids;
        }
    }
}
