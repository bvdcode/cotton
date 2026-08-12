// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record RestoreFileVersionRequest(
        Guid UserId,
        Guid NodeFileId,
        Guid VersionId) : IRequest<NodeFileManifestDto>;

    public class RestoreFileVersionRequestHandler(
        FileVersionService _versions,
        IEventNotificationService _notifications)
        : IRequestHandler<RestoreFileVersionRequest, NodeFileManifestDto>
    {
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
