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
        string? ExpectedETag) : IRequest<NodeFile?>;

    public class ResolveOwnedFileContentQueryHandler(
        CottonDbContext _dbContext,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<ResolveOwnedFileContentQuery, NodeFile?>
    {
        public async Task<NodeFile?> Handle(
            ResolveOwnedFileContentQuery request,
            CancellationToken ct)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles.AccessibleTo(request.UserId)
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(
                    x => x.Id == request.NodeFileId
                        && x.Node.Type == NodeType.Default,
                    ct);
            if (nodeFile is null)
            {
                return null;
            }

            _fileGraphIntegrity.RequireValidContent(
                _dbContext,
                nodeFile,
                "file.content");

            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before download.");
            }

            return nodeFile;
        }
    }
}
