// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record DeleteFileVersionRequest(
        Guid UserId,
        Guid NodeFileId,
        Guid VersionId) : IRequest;

    public class DeleteFileVersionRequestHandler(FileVersionService _versions)
        : IRequestHandler<DeleteFileVersionRequest>
    {
        public Task Handle(
            DeleteFileVersionRequest request,
            CancellationToken ct)
        {
            return _versions.DeleteVersionAsync(
                request.UserId,
                request.NodeFileId,
                request.VersionId,
                ct);
        }
    }
}
