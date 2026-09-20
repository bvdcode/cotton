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
using System.Diagnostics;

namespace Cotton.Server.Handlers.Files
{
    public record PrepareUpgradeTo06Request : IRequest;

    public class PrepareUpgradeTo06RequestHandler(
        CottonDbContext dbContext,
        IMediator mediator,
        ILogger<PrepareUpgradeTo06RequestHandler> logger)
        : IRequestHandler<PrepareUpgradeTo06Request>
    {
        public const string TargetVersion = "0.6";

        public async Task Handle(PrepareUpgradeTo06Request request, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Upgrade preparation for Cotton {TargetVersion} started", TargetVersion);
            int updated = await mediator.Send(new BackfillNodeFileContentTypesRequest(), cancellationToken);
            int cleared = await mediator.Send(new ClearFileManifestContentTypesRequest(), cancellationToken);
            logger.LogInformation("Checking empty file manifest preview data");
            int previewsCleared = await mediator.Send(new ClearEmptyFilePreviewRequest(), cancellationToken);
            if (previewsCleared > 0)
            {
                logger.LogInformation("Cleared preview data for the empty file manifest");
            }

            logger.LogInformation("Checking for remaining upgrade preparation work");
            bool pending = await dbContext.NodeFiles.AnyAsync(file => file.ContentType == string.Empty
                    || EF.Property<int?>(file, DatabaseIntegrityColumns.VersionProperty) != NodeFileIntegrityDescriptor.LatestVersion,
                    cancellationToken)
                || await dbContext.FileManifests.AnyAsync(manifest => manifest.ContentType != string.Empty
                    || EF.Property<int?>(manifest, DatabaseIntegrityColumns.VersionProperty) != FileManifestIntegrityDescriptor.LatestVersion,
                    cancellationToken)
                || await ClearEmptyFilePreviewRequestHandler.GetPendingManifests(dbContext).AnyAsync(cancellationToken);
            if (pending)
            {
                logger.LogInformation(
                    "Upgrade preparation has remaining rows and will continue on the next run. Elapsed: {ElapsedSeconds:F1}s",
                    stopwatch.Elapsed.TotalSeconds);
                return;
            }

            await mediator.Send(new NotifyUpgradePreparationCompletedRequest(
                nameof(PrepareUpgradeTo06Job), TargetVersion), cancellationToken);
            logger.LogInformation(
                "Upgrade preparation for Cotton {TargetVersion} finished. Updated {NodeFileCount} node files, {ManifestCount} file manifests, cleared {PreviewCount} empty file previews in {ElapsedSeconds:F1}s",
                TargetVersion, updated, cleared, previewsCleared, stopwatch.Elapsed.TotalSeconds);
        }
    }
}
