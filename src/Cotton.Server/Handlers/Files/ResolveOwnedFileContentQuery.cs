// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Topology;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record ResolveOwnedFileContentQuery(
        Guid UserId,
        Guid NodeFileId,
        string? ExpectedETag,
        int? ChunkNumber) : IRequest<ResolvedOwnedFileContent?>;

    public class ResolveOwnedFileContentQueryHandler(
        CottonDbContext _dbContext,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<ResolveOwnedFileContentQuery, ResolvedOwnedFileContent?>
    {
        public async Task<ResolvedOwnedFileContent?> Handle(
            ResolveOwnedFileContentQuery request,
            CancellationToken ct)
        {
            IQueryable<NodeFile> files = _dbContext.NodeFiles.AccessibleTo(request.UserId)
                .Include(x => x.Node)
                .Include(x => x.FileManifest);
            if (request.ChunkNumber is null)
            {
                files = files.Include(x => x.FileManifest.FileManifestChunks)
                    .ThenInclude(x => x.Chunk);
            }

            NodeFile? nodeFile = await files.SingleOrDefaultAsync(
                x => x.Id == request.NodeFileId
                    && x.Node.Type == NodeType.Default,
                ct);
            if (nodeFile is null)
            {
                return null;
            }

            if (request.ChunkNumber is null)
            {
                _fileGraphIntegrity.RequireValidContent(_dbContext, nodeFile, "file.content");
            }
            else
            {
                _fileGraphIntegrity.RequireValidMetadata(_dbContext, nodeFile, "file.content-chunk");
            }

            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before download.");
            }

            if (request.ChunkNumber is null)
            {
                return new ResolvedOwnedFileContent(nodeFile, null, nodeFile.FileManifest.FileManifestChunks.Count);
            }

            return await ResolveChunkAsync(nodeFile, request.ChunkNumber.Value, ct);
        }

        private async Task<ResolvedOwnedFileContent> ResolveChunkAsync(
            NodeFile nodeFile,
            int chunkNumber,
            CancellationToken ct)
        {
            int? lastChunkOrder = await _dbContext.FileManifestChunks
                .Where(x => x.FileManifestId == nodeFile.FileManifestId)
                .OrderByDescending(x => x.ChunkOrder)
                .Select(x => (int?)x.ChunkOrder)
                .FirstOrDefaultAsync(ct);
            int chunkCount = lastChunkOrder is null ? 0 : checked(lastChunkOrder.Value + 1);
            if (chunkNumber >= chunkCount)
            {
                return new ResolvedOwnedFileContent(nodeFile, null, chunkCount);
            }

            FileManifestChunk? chunk = await _dbContext.FileManifestChunks
                .Include(x => x.Chunk)
                .SingleOrDefaultAsync(x =>
                    x.FileManifestId == nodeFile.FileManifestId
                    && x.ChunkOrder == chunkNumber, ct);
            if (chunk is not null)
            {
                _fileGraphIntegrity.RequireValidManifestChunk(
                    _dbContext,
                    nodeFile.FileManifest,
                    chunk,
                    chunkNumber,
                    "file.content-chunk");
            }

            return new ResolvedOwnedFileContent(nodeFile, chunk, chunkCount);
        }
    }
}
