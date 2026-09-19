// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record ClearEmptyFilePreviewRequest : IRequest<int>;

    public class ClearEmptyFilePreviewRequestHandler(CottonDbContext dbContext)
        : IRequestHandler<ClearEmptyFilePreviewRequest, int>
    {
        public async Task<int> Handle(ClearEmptyFilePreviewRequest request, CancellationToken cancellationToken)
        {
            byte[] emptyHash = Hasher.FromHexStringHash(Hasher.ZeroHashHexString);
            FileManifest? manifest = await dbContext.FileManifests.SingleOrDefaultAsync(
                file => file.ProposedContentHash == emptyHash && file.SizeBytes == 0,
                cancellationToken);
            if (manifest is null)
            {
                return 0;
            }

            manifest.SmallFilePreviewHash = null;
            manifest.SmallFilePreviewHashEncrypted = null;
            manifest.LargeFilePreviewHash = null;
            manifest.PreviewGenerationError = null;
            manifest.PreviewGeneratorId = null;
            manifest.PreviewGeneratorVersion = PreviewGeneratorProvider.DefaultGeneratorVersion;
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
