// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
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
    /// <summary>
    /// Resolves and validates content for an owned file.
    /// </summary>
    public record ResolveOwnedFileContentQuery(
        Guid UserId,
        Guid NodeFileId,
        OwnedFileContentPurpose Purpose,
        string? ExpectedETag) : IRequest<NodeFile?>;

    /// <summary>
    /// Handles owned file content resolution.
    /// </summary>
    public class ResolveOwnedFileContentQueryHandler(
        CottonDbContext _dbContext,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<ResolveOwnedFileContentQuery, NodeFile?>
    {
        /// <summary>
        /// Loads the file graph and enforces its integrity and ETag precondition.
        /// </summary>
        public async Task<NodeFile?> Handle(
            ResolveOwnedFileContentQuery request,
            CancellationToken ct)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(
                    x => x.Id == request.NodeFileId
                        && x.OwnerId == request.UserId
                        && x.Node.Type == NodeType.Default,
                    ct);
            if (nodeFile is null)
            {
                return null;
            }

            string integrityOperation = request.Purpose switch
            {
                OwnedFileContentPurpose.Download => "file.content",
                OwnedFileContentPurpose.Manifest => "file.content-manifest",
                _ => throw new ArgumentOutOfRangeException(nameof(request.Purpose)),
            };
            _fileGraphIntegrity.RequireValidContent(
                _dbContext,
                nodeFile,
                integrityOperation);

            if (!FileETags.MatchesIfMatchHeader(request.ExpectedETag, nodeFile))
            {
                string message = request.Purpose switch
                {
                    OwnedFileContentPurpose.Download => "File content changed before download.",
                    OwnedFileContentPurpose.Manifest => "File content changed before manifest fetch.",
                    _ => throw new ArgumentOutOfRangeException(nameof(request.Purpose)),
                };
                throw new FilePreconditionFailedException<NodeFile>(message);
            }

            return nodeFile;
        }
    }
}
