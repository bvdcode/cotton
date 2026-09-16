// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Npgsql;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class BuildVectorIndexJob(
        IMediator mediator,
        VectorIndexBuildState state,
        ILogger<BuildVectorIndexJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            if (!state.GetSnapshot().Running)
            {
                return;
            }

            string? errorCode = null;
            try
            {
                errorCode = await mediator.Send(new BuildVectorIndexRequest(), context.CancellationToken);
                if (errorCode is not null)
                {
                    logger.LogWarning("Vector index preparation did not complete: {ErrorCode}.", errorCode);
                }
            }
            catch (PostgresException ex)
            {
                errorCode = ex.SqlState switch
                {
                    PostgresErrorCodes.InsufficientPrivilege => "pgvector_index_permission_denied",
                    PostgresErrorCodes.DataException => "pgvector_index_invalid_data",
                    _ => "pgvector_index_build_failed"
                };
                logger.LogError(ex, "Failed to prepare the vector index.");
                throw;
            }
            catch (Exception ex)
            {
                errorCode = "pgvector_index_build_failed";
                logger.LogError(ex, "Vector index preparation was interrupted.");
                throw;
            }
            finally
            {
                state.Complete(errorCode);
            }
        }
    }
}
