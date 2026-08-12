// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Restores a retained file version.
    /// </summary>
    public record RestoreFileVersionRequest(
        Guid UserId,
        Guid NodeFileId,
        Guid VersionId) : IRequest<NodeFileManifestDto>;

    /// <summary>
    /// Handles file version restore requests.
    /// </summary>
    public class RestoreFileVersionRequestHandler(
        FileVersionService _versions,
        IEventNotificationService _notifications)
        : IRequestHandler<RestoreFileVersionRequest, NodeFileManifestDto>
    {
        /// <inheritdoc />
        public async Task<NodeFileManifestDto> Handle(
            RestoreFileVersionRequest request,
            CancellationToken ct)
        {
            NodeFileManifestDto restored = await _versions.RestoreVersionAsync(
                request.UserId,
                request.NodeFileId,
                request.VersionId,
                ct);
            await _notifications.NotifyFileUpdatedAsync(restored, ct);
            return restored;
        }
    }
}
