// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Deletes an owned file and publishes its realtime notification.
    /// </summary>
    public record DeleteFileRequest(
        Guid UserId,
        Guid NodeFileId,
        bool SkipTrash,
        string? ExpectedETag = null) : IRequest<Guid>;

    /// <summary>
    /// Handles explicit file deletion requests.
    /// </summary>
    public class DeleteFileRequestHandler(
        IMediator _mediator,
        IEventNotificationService _notifications)
        : IRequestHandler<DeleteFileRequest, Guid>
    {
        /// <inheritdoc />
        public async Task<Guid> Handle(DeleteFileRequest request, CancellationToken ct)
        {
            Guid parentNodeId = await _mediator.Send(
                new DeleteFileQuery(
                    request.UserId,
                    request.NodeFileId,
                    request.SkipTrash,
                    request.ExpectedETag),
                ct);
            await _notifications.NotifyFileDeletedAsync(
                request.UserId,
                request.NodeFileId,
                parentNodeId,
                ct);
            return parentNodeId;
        }
    }
}
