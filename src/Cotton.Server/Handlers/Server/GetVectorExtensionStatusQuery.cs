// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quartz;

namespace Cotton.Server.Handlers.Server
{
    public class GetVectorExtensionStatusQuery : IRequest<VectorExtensionStatusDto>
    {
    }

    public class GetVectorExtensionStatusQueryHandler(
        CottonDbContext dbContext, IMediator mediator, ISchedulerFactory schedulerFactory)
        : IRequestHandler<GetVectorExtensionStatusQuery, VectorExtensionStatusDto>
    {
        public async Task<VectorExtensionStatusDto> Handle(
            GetVectorExtensionStatusQuery request,
            CancellationToken cancellationToken)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                NpgsqlConnection connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
                bool extensionEnabled = await dbContext.Database.IsExtensionInstalledAsync("vector", cancellationToken);
                bool extensionAvailable = await dbContext.Database.IsExtensionAvailableAsync("vector", cancellationToken);
                long vectorCount = await dbContext.FileEmbeddings.LongCountAsync(cancellationToken);
                var files = await dbContext.NodeFiles
                    .Where(file => file.Node.Layout.IsActive && file.Node.Type == NodeType.Default
                        && (file.OriginalNodeFileId == Guid.Empty || file.Id == file.OriginalNodeFileId))
                    .GroupBy(file => 1)
                    .Select(group => new
                    {
                        Total = group.LongCount(),
                        Embedded = group.LongCount(file => dbContext.FileEmbeddings.Any(embedding =>
                            embedding.FileManifestId == file.FileManifestId
                            && embedding.IndexVersion == VectorIndexDefinition.Version))
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                PostgresIndexStatus index = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
                bool building = index.IsBuilding || await IsBuildScheduledAsync(cancellationToken);
                string? errorCode = null;
                if (index.Exists && !index.IsCompatibleWith(VectorIndexDefinition.Expected))
                {
                    errorCode = "pgvector_index_incompatible";
                }
                else if (index.Exists && !index.IsValid && !building)
                {
                    errorCode = "pgvector_index_build_failed";
                }

                return new VectorExtensionStatusDto
                {
                    ExtensionEnabled = extensionEnabled,
                    ExtensionAvailable = extensionAvailable,
                    PostgresMajorVersion = connection.PostgreSqlVersion.Major,
                    DatabaseName = connection.Database,
                    VectorCount = vectorCount,
                    FileCount = files?.Total ?? 0,
                    EmbeddedFileCount = files?.Embedded ?? 0,
                    IndexReady = VectorIndexDefinition.IsReady(index),
                    IndexBuilding = building,
                    IndexSizeBytes = index.SizeBytes,
                    IndexErrorCode = VectorIndexDefinition.IsReady(index) ? null : errorCode,
                    IndexCreateSql = VectorIndexDefinition.ManualCreateSql
                };
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }

        private async Task<bool> IsBuildScheduledAsync(CancellationToken cancellationToken)
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            JobKey jobKey = new(nameof(BuildVectorIndexJob));
            IReadOnlyCollection<IJobExecutionContext> executing = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
            if (executing.Any(job => job.JobDetail.Key.Equals(jobKey)))
            {
                return true;
            }

            IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
            foreach (ITrigger trigger in triggers)
            {
                if (trigger.GetNextFireTimeUtc() is DateTimeOffset nextFire && nextFire <= DateTimeOffset.UtcNow
                    && await scheduler.GetTriggerState(trigger.Key, cancellationToken) is TriggerState.Normal or TriggerState.Blocked)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
