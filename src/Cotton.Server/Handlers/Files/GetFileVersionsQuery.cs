// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Gets retained versions of an owned file.
    /// </summary>
    public record GetFileVersionsQuery(
        Guid UserId,
        Guid NodeFileId) : IRequest<IReadOnlyList<FileVersionDto>>;

    /// <summary>
    /// Handles file version list queries.
    /// </summary>
    public class GetFileVersionsQueryHandler(FileVersionService _versions)
        : IRequestHandler<GetFileVersionsQuery, IReadOnlyList<FileVersionDto>>
    {
        /// <inheritdoc />
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
