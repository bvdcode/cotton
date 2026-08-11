// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Deletes an owned node and publishes its realtime notification.
    /// </summary>
    public record DeleteNodeRequest(
        Guid UserId,
        Guid NodeId,
        bool SkipTrash) : IRequest<Guid>;

    /// <summary>
    /// Handles explicit node deletion requests.
    /// </summary>
    public class DeleteNodeRequestHandler(
        IMediator _mediator,
        IEventNotificationService _notifications)
        : IRequestHandler<DeleteNodeRequest, Guid>
    {
        /// <inheritdoc />
        public async Task<Guid> Handle(DeleteNodeRequest request, CancellationToken ct)
        {
            Guid parentNodeId = await _mediator.Send(
                new DeleteNodeQuery(request.UserId, request.NodeId, request.SkipTrash),
                ct);
            await _notifications.NotifyNodeDeletedAsync(
                request.UserId,
                request.NodeId,
                parentNodeId,
                ct);
            return parentNodeId;
        }
    }
}
