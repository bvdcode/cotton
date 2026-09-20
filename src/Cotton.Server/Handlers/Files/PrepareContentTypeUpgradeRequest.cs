// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Server.Handlers.Notifications;
using Cotton.Server.Jobs;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record PrepareContentTypeUpgradeRequest : IRequest;

    public class PrepareContentTypeUpgradeRequestHandler(
        CottonDbContext dbContext,
        IMediator mediator,
        ILogger<PrepareContentTypeUpgradeRequestHandler> logger)
        : IRequestHandler<PrepareContentTypeUpgradeRequest>
    {
        public const string TargetVersion = "0.6";

        public async Task Handle(PrepareContentTypeUpgradeRequest request, CancellationToken cancellationToken)
        {
            int updated = await mediator.Send(new BackfillNodeFileContentTypesRequest(), cancellationToken);
            logger.LogInformation("Backfilled content types for {Count} node files", updated);
            int cleared = await mediator.Send(new ClearFileManifestContentTypesRequest(), cancellationToken);
            logger.LogInformation("Cleared obsolete content types and upgraded signatures for {Count} file manifests", cleared);

            bool pending = await dbContext.NodeFiles.AnyAsync(file => file.ContentType == string.Empty, cancellationToken)
                || await dbContext.FileManifests.AnyAsync(manifest => manifest.ContentType != string.Empty
                    || EF.Property<int?>(manifest, DatabaseIntegrityColumns.VersionProperty) != FileManifestIntegrityDescriptor.LatestVersion,
                    cancellationToken);
            if (pending)
            {
                logger.LogInformation("Content type upgrade preparation has remaining rows and will continue on the next run.");
                return;
            }

            await mediator.Send(new NotifyUpgradePreparationCompletedRequest(
                nameof(HotfixBackfillContentTypeJob), TargetVersion), cancellationToken);
        }
    }
}
