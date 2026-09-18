// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Computation;
using Cotton.Server.Services;
using Cotton.Server.Services.Computation;
using Cotton.Server.Services.Search;
using Cotton.TextExtraction;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class GenerateFileEmbeddingsJob(
        CottonDbContext dbContext, IMediator mediator, ComputationService computation,
        FileTextExtractorProvider extractors, PerfTracker perf, ILogger<GenerateFileEmbeddingsJob> logger) : IJob
    {
        private const int BatchSize = 32;
        private const int MaxItemsPerRun = 256;

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            try
            {
                if (perf.IsUploading() || !await dbContext.Database.IsExtensionInstalledAsync("vector", cancellationToken))
                {
                    return;
                }
                PostgresIndexStatus index = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
                if (!VectorIndexDefinition.IsReady(index))
                {
                    return;
                }
                string[] contentTypes = extractors.GetSupportedContentTypes();
                if (!await FileTextIndexQuery.Pending(dbContext, contentTypes).AnyAsync(cancellationToken))
                {
                    return;
                }
                ComputationStatus status = await computation.GetStatusAsync(cancellationToken: cancellationToken);
                if (!status.IsReady)
                {
                    return;
                }
                for (int processed = 0; processed < MaxItemsPerRun;)
                {
                    List<Guid> ids = await FileTextIndexQuery.Pending(dbContext, contentTypes)
                        .OrderBy(manifest => manifest.CreatedAt).ThenBy(manifest => manifest.Id)
                        .Select(manifest => manifest.Id).Take(Math.Min(BatchSize, MaxItemsPerRun - processed))
                        .ToListAsync(cancellationToken);
                    if (ids.Count == 0)
                    {
                        return;
                    }
                    foreach (Guid id in ids)
                    {
                        if (perf.IsUploading())
                        {
                            return;
                        }
                        try
                        {
                            await mediator.Send(new IndexFileTextRequest(id), cancellationToken);
                            processed++;
                        }
                        catch (DbUpdateConcurrencyException ex)
                        {
                            logger.LogInformation(ex, "Skipped stale text index update for file manifest {FileManifestId}.", id);
                            processed++;
                        }
                        finally
                        {
                            dbContext.ChangeTracker.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "File text indexing did not complete.");
                throw;
            }
        }
    }
}
