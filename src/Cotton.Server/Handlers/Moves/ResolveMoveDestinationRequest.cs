// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Moves
{
    public record ResolveMoveDestinationRequest(
        Guid UserId,
        Guid ParentId,
        string NameKey,
        Guid SourceId,
        MoveOverwritePolicy Overwrite) : IRequest<bool>;

    public class ResolveMoveDestinationRequestHandler(TrashRestoreCoordinator conflicts)
        : IRequestHandler<ResolveMoveDestinationRequest, bool>
    {
        public async Task<bool> Handle(ResolveMoveDestinationRequest request, CancellationToken ct)
        {
            TrashRestoreCoordinator.ConflictInfo? conflict = await conflicts.FindConflictAsync(
                request.UserId, request.ParentId, request.NameKey, ct);
            if (!conflict.HasValue || conflict.Value.Id == request.SourceId)
            {
                return false;
            }

            bool replace;
            switch (request.Overwrite)
            {
                case MoveOverwritePolicy.None:
                    replace = false;
                    break;
                case MoveOverwritePolicy.Files:
                    replace = conflict.Value.Kind == RestoreConflictKind.File;
                    break;
                case MoveOverwritePolicy.All:
                    replace = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Overwrite));
            }
            if (!replace)
            {
                throw new DuplicateException(request.NameKey, extra: new { conflictKind = conflict.Value.Kind });
            }

            await conflicts.SendConflictToTrashAsync(request.UserId, conflict.Value, ct);
            return true;
        }
    }
}
