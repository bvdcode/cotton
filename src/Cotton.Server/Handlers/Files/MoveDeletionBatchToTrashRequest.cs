// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Cotton.Topology.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record MoveDeletionBatchToTrashRequest(Guid UserId, DeletionBatch Batch) : IRequest<bool>;

    public class MoveDeletionBatchToTrashRequestHandler(
        CottonDbContext _db, ILayoutService _layouts, IMediator _mediator, ISyncChangeRecorder _sync)
        : IRequestHandler<MoveDeletionBatchToTrashRequest, bool>
    {
        public async Task<bool> Handle(MoveDeletionBatchToTrashRequest request, CancellationToken ct)
        {
            DeletionBatch batch = request.Batch;
            Guid[] parentIds = [.. batch.Folders.Select(x => x.ParentId!.Value)
                .Concat(batch.Files.Select(x => x.NodeId)).Distinct()];
            IReadOnlyDictionary<Guid, IReadOnlyList<TrashParent>> parents = await _mediator.Send(
                new CaptureBatchTrashParentsQuery(request.UserId, parentIds), ct);
            IReadOnlyList<Node> wrappers = await _layouts.CreateTrashItemsAsync(
                request.UserId, batch.Folders.Count + batch.Files.Count, ct);
            Guid[] subtreeIds = [.. batch.Subtree.Select(x => x.Id)];
            Guid[] fileIds = [.. batch.Files.Select(x => x.Id)];
            DateTime now = DateTime.UtcNow;
            List<DownloadToken> tokens = await _db.DownloadTokens.Where(x => x.CreatedByUserId == request.UserId
                    && (fileIds.Contains(x.NodeFileId) || subtreeIds.Contains(x.NodeFile.NodeId))
                    && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .ToListAsync(ct);
            foreach (DownloadToken token in tokens)
            {
                token.ExpiresAt = now;
            }
            int wrapperIndex = 0;
            foreach (Node folder in batch.Folders)
            {
                _sync.StageFolderChange(SyncChangeKind.FolderDeleted, folder, folder.ParentId!.Value);
                folder.Metadata = TrashParentMetadata.Write(folder.Metadata, parents[folder.ParentId.Value]);
                folder.SetParent(wrappers[wrapperIndex++], NodeType.Trash);
            }
            foreach (Node node in batch.Subtree)
            {
                node.Type = NodeType.Trash;
            }
            foreach (NodeFile file in batch.Files)
            {
                _sync.StageFileChange(SyncChangeKind.FileDeleted, file, file.Node.LayoutId);
                file.Metadata = TrashParentMetadata.Write(file.Metadata, parents[file.NodeId]);
                file.NodeId = wrappers[wrapperIndex++].Id;
            }
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
