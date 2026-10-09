// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record PermanentlyDeleteBatchRequest(Guid UserId, DeletionBatch Batch) : IRequest<long>;

    public class PermanentlyDeleteBatchRequestHandler(CottonDbContext _db, ISyncChangeRecorder _sync)
        : IRequestHandler<PermanentlyDeleteBatchRequest, long>
    {
        public async Task<long> Handle(PermanentlyDeleteBatchRequest request, CancellationToken ct)
        {
            DeletionBatch batch = request.Batch;
            HashSet<Guid> subtreeIds = [.. batch.Subtree.Select(x => x.Id)];
            Guid[] fileIds = [.. batch.Files.Select(x => x.Id)];
            Guid[] wrapperIds = [.. batch.Files.Where(x => x.Node.Type == NodeType.Trash && !subtreeIds.Contains(x.NodeId))
                .Select(x => x.NodeId).Distinct()];
            await _db.NodeShareTokens.Where(x => subtreeIds.Contains(x.NodeId)).ExecuteDeleteAsync(ct);
            await _db.DownloadTokens.Where(x => fileIds.Contains(x.NodeFileId)).ExecuteDeleteAsync(ct);
            foreach (Node folder in batch.Folders.Where(x => x.Type == NodeType.Default))
            {
                _sync.StageFolderChange(SyncChangeKind.FolderDeleted, folder, folder.ParentId!.Value);
            }
            foreach (NodeFile file in batch.Files.Where(x => batch.SelectedFileIds.Contains(x.Id)
                && x.Node.Type == NodeType.Default && !subtreeIds.Contains(x.NodeId)))
            {
                _sync.StageFileChange(SyncChangeKind.FileDeleted, file, file.Node.LayoutId);
            }
            long removedBytes = batch.Files.Sum(x => x.FileManifest.SizeBytes);
            _db.NodeFiles.RemoveRange(batch.Files);
            _db.Nodes.RemoveRange(batch.Subtree);
            await _db.SaveChangesAsync(ct);
            List<Node> emptyWrappers = await _db.Nodes
                .Where(x => x.OwnerId == request.UserId && x.Type == NodeType.Trash && x.ParentId != null
                    && wrapperIds.Contains(x.Id) && !x.Children.Any() && !x.NodeFiles.Any())
                .ToListAsync(ct);
            if (emptyWrappers.Count > 0)
            {
                Guid[] emptyIds = [.. emptyWrappers.Select(x => x.Id)];
                await _db.NodeShareTokens.Where(x => emptyIds.Contains(x.NodeId)).ExecuteDeleteAsync(ct);
                _db.Nodes.RemoveRange(emptyWrappers);
                await _db.SaveChangesAsync(ct);
            }
            return removedBytes;
        }
    }
}
