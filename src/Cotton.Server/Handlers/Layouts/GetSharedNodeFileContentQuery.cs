// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeFileContentQuery(
        string Token,
        Guid NodeFileId,
        bool Preview) : IRequest<GetSharedNodeFileContentResult>;

    public class GetSharedNodeFileContentQueryHandler(
        IMediator _mediator,
        CottonDbContext _dbContext,
        FileGraphIntegrityVerifier _fileGraphIntegrity)
        : IRequestHandler<GetSharedNodeFileContentQuery, GetSharedNodeFileContentResult>
    {
        public async Task<GetSharedNodeFileContentResult> Handle(
            GetSharedNodeFileContentQuery request,
            CancellationToken ct)
        {
            SharedNodeAccess? access = await _mediator.Send(
                new ResolveSharedNodeAccessQuery(request.Token),
                ct);
            if (access is null)
            {
                return NotFound(GetSharedNodeFileContentStatus.SharedFolderNotFound);
            }

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(
                    x => x.Id == request.NodeFileId
                        && x.OwnerId == access.CreatedByUserId,
                    ct);
            if (nodeFile is null)
            {
                return NotFound(GetSharedNodeFileContentStatus.FileNotFound);
            }

            bool servesPreview = request.Preview
                && nodeFile.FileManifest.LargeFilePreviewHash is not null;
            RequireIntegrity(nodeFile, servesPreview);

            if (nodeFile.Node.Type != NodeType.Default)
            {
                return NotFound(GetSharedNodeFileContentStatus.FileNotFound);
            }

            bool canAccessFile = await _mediator.Send(
                new VerifySharedNodeSubtreeAccessQuery(
                    nodeFile.NodeId,
                    access.NodeId,
                    access.CreatedByUserId),
                ct);
            if (!canAccessFile)
            {
                return NotFound(GetSharedNodeFileContentStatus.FileNotFound);
            }

            return new GetSharedNodeFileContentResult(
                GetSharedNodeFileContentStatus.Success,
                nodeFile,
                servesPreview);
        }

        private static GetSharedNodeFileContentResult NotFound(
            GetSharedNodeFileContentStatus status) => new(status);

        private void RequireIntegrity(NodeFile nodeFile, bool servesPreview)
        {
            if (servesPreview)
            {
                _fileGraphIntegrity.RequireValidMetadata(
                    _dbContext,
                    nodeFile,
                    "shared-folder.preview");
                return;
            }

            _fileGraphIntegrity.RequireValidContent(
                _dbContext,
                nodeFile,
                "shared-folder.download");
        }
    }
}
