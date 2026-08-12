// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Files
{
    public record RenameFileRequest(
        Guid UserId,
        Guid NodeFileId,
        string Name,
        string? ExpectedETag) : IRequest<RenameFileResult>;

    public class RenameFileRequestHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        ILayoutMutationGate _layoutGate,
        IEventNotificationService _notifications)
        : IRequestHandler<RenameFileRequest, RenameFileResult>
    {
        public async Task<RenameFileResult> Handle(
            RenameFileRequest request,
            CancellationToken ct)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(
                request.Name,
                out _,
                out string? errorMessage);
            if (!isValidName)
            {
                return Failure(RenameFileStatus.InvalidName, errorMessage);
            }

            Guid? layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync(ct);
            if (layoutId is null)
            {
                return Failure(RenameFileStatus.FileNotFound, "File not found.");
            }

            NodeFileManifestDto file;
            await using (IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(
                layoutId.Value,
                ct))
            await using (IDbContextTransaction tx = await _dbContext.Database
                .BeginTransactionAsync(ct))
            {
                NodeFile? nodeFile = await _dbContext.NodeFiles
                    .Include(x => x.Node)
                    .Include(x => x.FileManifest)
                    .Where(x => x.Id == request.NodeFileId
                        && x.OwnerId == request.UserId)
                    .SingleOrDefaultAsync(ct);
                if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
                {
                    return Failure(RenameFileStatus.FileNotFound, "File not found.");
                }

                if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
                {
                    throw new FilePreconditionFailedException<NodeFile>(
                        "File content changed before rename.");
                }

                string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
                string? conflict = await FindNameConflictAsync(
                    nodeFile,
                    request,
                    nameKey,
                    ct);
                if (conflict is not null)
                {
                    return Failure(RenameFileStatus.NameConflict, conflict);
                }

                nodeFile.SetName(request.Name);
                _syncChanges.StageFileChange(
                    SyncChangeKind.FileRenamed,
                    nodeFile,
                    nodeFile.Node.LayoutId);
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                file = nodeFile.Adapt<NodeFileManifestDto>();
            }

            await _notifications.NotifyFileRenamedAsync(file, ct);
            return new RenameFileResult(
                RenameFileStatus.Renamed,
                file);
        }

        private async Task<string?> FindNameConflictAsync(
            NodeFile nodeFile,
            RenameFileRequest request,
            string nameKey,
            CancellationToken ct)
        {
            bool fileExists = await _dbContext.NodeFiles.AnyAsync(
                x => x.NodeId == nodeFile.NodeId
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey
                    && x.Id != request.NodeFileId,
                ct);
            if (fileExists)
            {
                return "A file with the same name key already exists in this folder: " + nameKey;
            }

            bool nodeExists = await _dbContext.Nodes.AnyAsync(
                x => x.ParentId == nodeFile.NodeId
                    && x.OwnerId == request.UserId
                    && x.Type == nodeFile.Node.Type
                    && x.NameKey == nameKey,
                ct);
            return nodeExists
                ? "A folder with the same name key already exists in this folder: " + nameKey
                : null;
        }

        private static RenameFileResult Failure(
            RenameFileStatus status,
            string? error) => new(status, Error: error);
    }
}
