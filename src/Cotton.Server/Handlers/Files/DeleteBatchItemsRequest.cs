// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Files;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    public record DeleteBatchItemsRequest(Guid UserId, IReadOnlyList<BatchItemRequestDto> Items, bool SkipTrash)
        : IRequest<IReadOnlyList<BatchItemResultDto>>;

    public class DeleteBatchItemsRequestHandler(
        CottonDbContext _db, IMediator _mediator, ILayoutMutationGate _layoutGate,
        UserStorageQuotaService _quota, ILogger<DeleteBatchItemsRequestHandler> _logger)
        : IRequestHandler<DeleteBatchItemsRequest, IReadOnlyList<BatchItemResultDto>>
    {
        public async Task<IReadOnlyList<BatchItemResultDto>> Handle(DeleteBatchItemsRequest request, CancellationToken ct)
        {
            Guid[] folderIds = [.. request.Items.Where(x => x.Kind == BatchItemKind.Folder).Select(x => x.Id)];
            Guid[] fileIds = [.. request.Items.Where(x => x.Kind == BatchItemKind.File).Select(x => x.Id)];
            Guid[] layoutIds = await _db.Nodes.AsNoTracking()
                .Where(x => x.OwnerId == request.UserId && folderIds.Contains(x.Id)).Select(x => x.LayoutId)
                .Union(_db.NodeFiles.Where(x => x.OwnerId == request.UserId && fileIds.Contains(x.Id)).Select(x => x.Node.LayoutId))
                .Distinct().OrderBy(x => x).ToArrayAsync(ct);
            List<IAsyncDisposable> leases = [];
            try
            {
                foreach (Guid layoutId in layoutIds)
                {
                    leases.Add(await _layoutGate.EnterAsync(layoutId, ct));
                }
                return await DeleteAsync(request, ct);
            }
            finally
            {
                for (int index = leases.Count - 1; index >= 0; index--)
                {
                    await leases[index].DisposeAsync();
                }
            }
        }

        private async Task<IReadOnlyList<BatchItemResultDto>> DeleteAsync(DeleteBatchItemsRequest request, CancellationToken ct)
        {
            await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);
            DeletionBatch batch = await _mediator.Send(new LoadDeletionBatchQuery(request.UserId, request.Items, request.SkipTrash), ct);
            long removedBytes = 0;
            if (batch.Folders.Count + batch.Files.Count > 0)
            {
                if (request.SkipTrash)
                {
                    removedBytes = await _mediator.Send(new PermanentlyDeleteBatchRequest(request.UserId, batch), ct);
                }
                else
                {
                    await _mediator.Send(new MoveDeletionBatchToTrashRequest(request.UserId, batch), ct);
                }
            }
            await tx.CommitAsync(ct);
            _quota.RecordLogicalBytesRemoved(request.UserId, removedBytes);
            _logger.LogInformation("User {UserId} deleted {Folders} folders and {Files} files; permanent: {Permanent}.",
                request.UserId, batch.SelectedFolderIds.Count, batch.SelectedFileIds.Count, request.SkipTrash);
            return request.Items.Select(item =>
            {
                bool deleted = item.Kind switch
                {
                    BatchItemKind.Folder => batch.SelectedFolderIds.Contains(item.Id),
                    BatchItemKind.File => batch.SelectedFileIds.Contains(item.Id),
                    _ => false,
                };
                return new BatchItemResultDto { Id = item.Id, Kind = item.Kind, Deleted = deleted, Failed = !deleted };
            }).ToList();
        }
    }
}
