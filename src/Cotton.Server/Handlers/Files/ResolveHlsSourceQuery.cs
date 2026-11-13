// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record ResolveHlsSourceQuery(
        Guid NodeFileId,
        FileDownloadCredentials Credentials) : IRequest<ResolveHlsSourceResult>;

    public class ResolveHlsSourceQueryHandler(
        CottonDbContext _dbContext,
        IMediator _mediator,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<ResolveHlsSourceQuery, ResolveHlsSourceResult>
    {
        public async Task<ResolveHlsSourceResult> Handle(
            ResolveHlsSourceQuery request,
            CancellationToken ct)
        {
            FileDownloadAccess? access = await _mediator.Send(
                new ResolveFileDownloadAccessQuery(request.NodeFileId, request.Credentials), ct);
            if (access is null)
            {
                return Failure(ResolveHlsSourceStatus.TokenNotFound);
            }

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == request.NodeFileId && x.OwnerId == access.UserId, ct);
            if (nodeFile is null)
            {
                return Failure(ResolveHlsSourceStatus.FileNotFound);
            }

            _fileGraphIntegrity.RequireValidContent(
                _dbContext,
                nodeFile,
                "file.hls-source");
            if (nodeFile.Node.Type != NodeType.Default)
            {
                return Failure(ResolveHlsSourceStatus.FileNotFound);
            }

            VideoPlaybackMode playbackMode = VideoPlaybackResolver.Resolve(
                nodeFile.ContentType,
                hasPreview: nodeFile.FileManifest.SmallFilePreviewHash is not null);
            if (playbackMode != VideoPlaybackMode.Transcode)
            {
                return Failure(ResolveHlsSourceStatus.NotTranscodable);
            }

            return new ResolveHlsSourceResult(
                ResolveHlsSourceStatus.Success,
                nodeFile,
                access.ShareToken);
        }

        private static ResolveHlsSourceResult Failure(
            ResolveHlsSourceStatus status) => new(status);
    }
}
