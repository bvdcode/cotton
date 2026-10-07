// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Nodes;
using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    public record CopyFileCommand(Guid UserId, Guid FileId, Guid ParentId, string? Name, bool Overwrite,
        Dictionary<string, string?>? Metadata = null)
        : IRequest<NodeFileManifestDto>;

    public class CopyFileCommandHandler(
        CottonDbContext _dbContext,
        IMediator _mediator,
        ILayoutMutationGate _layoutGate,
        UserStorageQuotaService _quota,
        ISyncChangeRecorder _syncChanges,
        IEventNotificationService _events,
        ILogger<CopyFileCommandHandler> _logger) : IRequestHandler<CopyFileCommand, NodeFileManifestDto>
    {
        public async Task<NodeFileManifestDto> Handle(CopyFileCommand request, CancellationToken ct)
        {
            Guid layoutId = await _dbContext.NodeFiles.AsNoTracking()
                .Where(x => x.Id == request.FileId && x.OwnerId == request.UserId && x.Node.Type == NodeType.Default)
                .Select(x => (Guid?)x.Node.LayoutId).SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<NodeFile>("Source file not found.");
            await using IAsyncDisposable gate = await _layoutGate.EnterAsync(layoutId, ct);
            NodeFile copy;
            await using (IAsyncDisposable quotaGate = await _quota.EnterMutationAsync(request.UserId, ct))
            await using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct))
            {
                NodeFile source = await _dbContext.NodeFiles.Include(x => x.FileManifest)
                    .SingleOrDefaultAsync(x => x.Id == request.FileId && x.OwnerId == request.UserId
                        && x.Node.Type == NodeType.Default && x.Node.LayoutId == layoutId, ct)
                    ?? throw new EntityNotFoundException<NodeFile>("Source file not found.");
                Node parent = await _mediator.Send(new PrepareCopyDestinationRequest(
                    request.UserId, layoutId, request.ParentId, request.Name ?? source.Name,
                    request.Overwrite, source.Id), ct);
                long addedBytes = await _quota.EnsureCanAddFileReferenceAsync(request.UserId, source.FileManifestId, ct);
                copy = source.CopyTo(parent, request.Name ?? source.Name);
                copy.Metadata = NodeMetadataPatch.Apply(copy.Metadata, request.Metadata);
                copy.FileManifest = source.FileManifest;
                source.FileManifest.ResetFailedPreview();
                _dbContext.NodeFiles.Add(copy);
                _syncChanges.StageFileChange(SyncChangeKind.FileCreated, copy, layoutId);
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                _quota.RecordLogicalBytesAdded(request.UserId, addedBytes);
            }
            try
            {
                await _events.NotifyFileCreatedAsync(copy.Id, ct);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Copy notification failed for file {FileId}", copy.Id);
            }
            return copy.Adapt<NodeFileManifestDto>();
        }
    }
}
