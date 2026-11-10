// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Server.Extensions;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Moves
{
    public record RelocateFileRequest(
        NodeFile File,
        Node Parent,
        string? Name = null,
        Dictionary<string, string?>? Metadata = null,
        MoveOverwritePolicy Overwrite = MoveOverwritePolicy.None) : IRequest<bool>;

    public class RelocateFileRequestHandler(
        CottonDbContext db,
        ISyncChangeRecorder syncChanges,
        IMediator mediator) : IRequestHandler<RelocateFileRequest, bool>
    {
        public async Task<bool> Handle(RelocateFileRequest request, CancellationToken ct)
        {
            NodeFile file = request.File;
            Node parent = request.Parent;
            if (parent.OwnerId != file.OwnerId || parent.LayoutId != file.Node.LayoutId
                || parent.Type != file.Node.Type)
            {
                throw new BadRequestException<Node>("Cannot move a file across layouts or node types.");
            }
            string name = request.Name ?? file.Name;
            if (!NameValidator.TryNormalizeAndValidate(name, out string normalized, out string error))
            {
                throw new BadRequestException<NodeFile>(error);
            }
            bool replaced = await mediator.Send(new ResolveMoveDestinationRequest(
                file.OwnerId, parent.Id, NameValidator.GetNameKey(normalized), file.Id, request.Overwrite), ct);

            Guid oldParentId = file.NodeId;
            file.NodeId = parent.Id;
            file.SetName(normalized);
            file.Metadata = NodeMetadataPatch.Apply(file.Metadata, request.Metadata);
            file.FileManifest.ResetFailedPreview();
            syncChanges.StageFileChange(SyncChangeKind.FileMoved, file, parent.LayoutId, oldParentId);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DuplicateException(file.NameKey);
            }
            return replaced;
        }
    }
}
