// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Server.Services;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Nodes
{
    public record PrepareCopyDestinationRequest(
        Guid UserId, Guid LayoutId, Guid ParentId, string Name,
        bool Overwrite = false, Guid? SourceFileId = null) : IRequest<Node>;

    public class PrepareCopyDestinationRequestHandler(
        CottonDbContext _dbContext,
        TrashRestoreCoordinator _conflicts) : IRequestHandler<PrepareCopyDestinationRequest, Node>
    {
        public async Task<Node> Handle(PrepareCopyDestinationRequest request, CancellationToken ct)
        {
            if (!NameValidator.TryNormalizeAndValidate(request.Name, out string name, out string error))
            {
                throw new BadRequestException<Node>(error);
            }
            Node parent = await _dbContext.Nodes.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.ParentId && x.OwnerId == request.UserId
                    && x.Type == NodeType.Default && x.LayoutId == request.LayoutId, ct)
                ?? throw new EntityNotFoundException<Node>("Destination folder not found in the source layout.");

            string nameKey = NameValidator.GetNameKey(name);
            TrashRestoreCoordinator.ConflictInfo? conflict = await _conflicts.FindConflictAsync(
                request.UserId, parent.Id, nameKey, ct);
            if (conflict.HasValue)
            {
                if (!request.Overwrite || conflict.Value.Kind != RestoreConflictKind.File
                    || conflict.Value.Id == request.SourceFileId)
                {
                    throw new DuplicateException(nameKey, extra: new { conflictKind = conflict.Value.Kind });
                }
                await _conflicts.SendConflictToTrashAsync(request.UserId, conflict.Value, ct);
            }
            return parent;
        }
    }
}
