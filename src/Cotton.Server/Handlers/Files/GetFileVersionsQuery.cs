// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record GetFileVersionsQuery(
        Guid UserId,
        Guid NodeFileId) : IRequest<IReadOnlyList<FileVersionDto>>;

    public class GetFileVersionsQueryHandler(FileVersionService _versions)
        : IRequestHandler<GetFileVersionsQuery, IReadOnlyList<FileVersionDto>>
    {
        public Task<IReadOnlyList<FileVersionDto>> Handle(
            GetFileVersionsQuery request,
            CancellationToken ct)
        {
            return _versions.ListVersionsAsync(
                request.UserId,
                request.NodeFileId,
                ct);
        }
    }
}
