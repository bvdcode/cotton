// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record CreateFileVersionDownloadLinkRequest(
        Guid UserId,
        Guid NodeFileId,
        Guid VersionId,
        int ExpireAfterMinutes) : IRequest<string>;

    public class CreateFileVersionDownloadLinkRequestHandler(
        FileVersionService _versions,
        FileDownloadTokenService tokens)
        : IRequestHandler<CreateFileVersionDownloadLinkRequest, string>
    {
        public async Task<string> Handle(
            CreateFileVersionDownloadLinkRequest request,
            CancellationToken ct)
        {
            const int maximumMinutes = 60 * 24 * 365;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ExpireAfterMinutes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.ExpireAfterMinutes, maximumMinutes);
            NodeFile version = await _versions.GetDownloadVersionAsync(
                request.UserId, request.NodeFileId, request.VersionId, ct);
            string token = tokens.Create(request.UserId, version.Id,
                TimeSpan.FromMinutes(request.ExpireAfterMinutes));
            return FileDownloadToken.BuildUrl(version.Id, token);
        }
    }
}
