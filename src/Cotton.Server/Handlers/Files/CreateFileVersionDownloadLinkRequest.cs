// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Creates a download link for a retained file version.
    /// </summary>
    public record CreateFileVersionDownloadLinkRequest(
        Guid UserId,
        Guid NodeFileId,
        Guid VersionId,
        int ExpireAfterMinutes) : IRequest<string>;

    /// <summary>
    /// Handles retained version download-link requests.
    /// </summary>
    public class CreateFileVersionDownloadLinkRequestHandler(
        FileVersionService _versions)
        : IRequestHandler<CreateFileVersionDownloadLinkRequest, string>
    {
        /// <inheritdoc />
        public Task<string> Handle(
            CreateFileVersionDownloadLinkRequest request,
            CancellationToken ct)
        {
            return _versions.CreateVersionDownloadLinkAsync(
                request.UserId,
                request.NodeFileId,
                request.VersionId,
                request.ExpireAfterMinutes,
                ct);
        }
    }
}
