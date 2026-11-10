// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Moves;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Nodes
{
    public class MoveNodeCommand : IRequest<NodeDto>
    {
        public Guid NodeId { get; set; }

        public Guid ParentId { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, string?>? Metadata { get; set; }

        public Guid UserId { get; set; }
    }

    public class MoveNodeCommandHandler(
        CottonDbContext db,
        IMediator mediator,
        IEventNotificationService notifications,
        ILayoutMutationGate layoutGate,
        CommittedMutationEffects effects) : IRequestHandler<MoveNodeCommand, NodeDto>
    {
        public async Task<NodeDto> Handle(MoveNodeCommand request, CancellationToken ct)
        {
            if (request.ParentId == Guid.Empty)
            {
                throw new BadRequestException<Node>("Target parent id is required.");
            }
            if (request.ParentId == request.NodeId)
            {
                throw new BadRequestException<Node>("Cannot move a node into itself.");
            }
            Guid layoutId = await db.Nodes.AsNoTracking()
                .Where(node => node.Id == request.NodeId && node.OwnerId == request.UserId)
                .Select(node => (Guid?)node.LayoutId).SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<Node>("Source folder not found.");

            await using IAsyncDisposable gate = await layoutGate.EnterAsync(layoutId, ct);
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            Node folder = await db.Nodes.SingleOrDefaultAsync(node => node.Id == request.NodeId
                && node.OwnerId == request.UserId, ct)
                ?? throw new EntityNotFoundException<Node>("Source folder not found.");
            if (folder.Type != NodeType.Default)
            {
                throw new EntityNotFoundException<Node>("Source folder is not in the active layout.");
            }
            if (folder.ParentId is null)
            {
                throw new AccessDeniedException<Node>("Cannot move the root node.");
            }
            if (folder.ParentId == request.ParentId)
            {
                return folder.Adapt<NodeDto>();
            }
            Node parent = await db.Nodes.SingleOrDefaultAsync(node => node.Id == request.ParentId
                && node.OwnerId == request.UserId, ct)
                ?? throw new EntityNotFoundException<Node>("Destination folder not found.");
            Guid oldParentId = folder.ParentId.Value;
            await mediator.Send(new RelocateFolderRequest(folder, parent, request.Name, request.Metadata), ct);
            await transaction.CommitAsync(ct);
            await effects.RunAsync(nameof(IEventNotificationService.NotifyNodeMovedAsync),
                () => notifications.NotifyNodeMovedAsync(folder.Id, oldParentId, CancellationToken.None));
            return folder.Adapt<NodeDto>();
        }
    }
}
