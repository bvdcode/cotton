// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record ResolveHlsSourceQuery(
        Guid NodeFileId,
        string Token) : IRequest<ResolveHlsSourceResult>;

    public class ResolveHlsSourceQueryHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<ResolveHlsSourceQuery, ResolveHlsSourceResult>
    {
        public async Task<ResolveHlsSourceResult> Handle(
            ResolveHlsSourceQuery request,
            CancellationToken ct)
        {
            DownloadToken? downloadToken = await _dbContext.DownloadTokens
                .FindActiveAsync(request.Token, request.NodeFileId, ct);
            if (downloadToken is null)
            {
                return Failure(ResolveHlsSourceStatus.TokenNotFound);
            }

            _integrity.RequireValid(_dbContext, downloadToken, "file.hls-token");

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == request.NodeFileId, ct);
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
                nodeFile.FileManifest.ContentType,
                hasPreview: nodeFile.FileManifest.SmallFilePreviewHash is not null);
            if (playbackMode != VideoPlaybackMode.Transcode)
            {
                return Failure(ResolveHlsSourceStatus.NotTranscodable);
            }

            return new ResolveHlsSourceResult(
                ResolveHlsSourceStatus.Success,
                nodeFile,
                downloadToken);
        }

        private static ResolveHlsSourceResult Failure(
            ResolveHlsSourceStatus status) => new(status);
    }
}
