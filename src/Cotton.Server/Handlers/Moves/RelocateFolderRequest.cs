// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Server.Extensions;
using Cotton.Topology;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Moves
{
    public record RelocateFolderRequest(
        Node Folder,
        Node Parent,
        string? Name = null,
        Dictionary<string, string?>? Metadata = null,
        MoveOverwritePolicy Overwrite = MoveOverwritePolicy.None) : IRequest<bool>;

    public class RelocateFolderRequestHandler(
        CottonDbContext db,
        ISyncChangeRecorder syncChanges,
        IMediator mediator,
        ILogger<RelocateFolderRequestHandler> logger) : IRequestHandler<RelocateFolderRequest, bool>
    {
        public async Task<bool> Handle(RelocateFolderRequest request, CancellationToken ct)
        {
            Node folder = request.Folder;
            Node parent = request.Parent;
            if (folder.ParentId is null)
            {
                throw new AccessDeniedException<Node>("Cannot move the root node.");
            }
            if (parent.OwnerId != folder.OwnerId || parent.LayoutId != folder.LayoutId || parent.Type != folder.Type)
            {
                throw new BadRequestException<Node>("Cannot move a folder across layouts or node types.");
            }
            await EnsureValidHierarchyAsync(folder, parent, ct);
            string name = request.Name ?? folder.Name;
            if (!NameValidator.TryNormalizeAndValidate(name, out string normalized, out string error))
            {
                throw new BadRequestException<Node>(error);
            }
            bool replaced = await mediator.Send(new ResolveMoveDestinationRequest(
                folder.OwnerId, parent.Id, NameValidator.GetNameKey(normalized), folder.Id, request.Overwrite), ct);

            Guid oldParentId = folder.ParentId.Value;
            folder.SetParent(parent);
            folder.SetName(normalized);
            folder.Metadata = NodeMetadataPatch.Apply(folder.Metadata, request.Metadata);
            syncChanges.StageFolderChange(SyncChangeKind.FolderMoved, folder, parent.Id, oldParentId);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DuplicateException(folder.NameKey);
            }
            return replaced;
        }

        private async Task EnsureValidHierarchyAsync(Node folder, Node parent, CancellationToken ct)
        {
            if (folder.Id == parent.Id)
            {
                throw new InvalidFolderMoveException("Cannot move a node into itself.");
            }
            IQueryable<Node> nodes = db.Nodes.AsNoTracking().AccessibleTo(folder.OwnerId);
            try
            {
                await foreach (var node in NodeHierarchy.ReadAncestorsAsync(
                    nodes, parent.Id, node => new { node.Id, node.ParentId }, node => node.ParentId,
                    folder.Id, int.MaxValue, ct))
                {
                    if (node.Id == folder.Id || node.ParentId == folder.Id)
                    {
                        throw new InvalidFolderMoveException("Cannot move a folder into its descendant.");
                    }
                }
            }
            catch (NodeHierarchyException exception)
            {
                logger.LogWarning(exception, "Cannot move a folder within an invalid hierarchy");
                throw new InvalidFolderMoveException("Folder hierarchy contains a cycle.");
            }
        }
    }
}
