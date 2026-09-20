// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Npgsql;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class BuildVectorIndexJob(
        IMediator mediator,
        ILogger<BuildVectorIndexJob> logger) : IJob
    {
        private static volatile string? _lastErrorCode;

        public static string? LastErrorCode => _lastErrorCode;

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                string? errorCode = await mediator.Send(new BuildVectorIndexRequest(), context.CancellationToken);
                _lastErrorCode = errorCode;
                if (errorCode is not null)
                {
                    logger.LogWarning("Vector index preparation did not complete: {ErrorCode}.", errorCode);
                }
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _lastErrorCode = ex switch
                {
                    PostgresException { SqlState: PostgresErrorCodes.InsufficientPrivilege } => "pgvector_index_permission_denied",
                    _ => "pgvector_index_build_failed"
                };
                logger.LogError(ex, "Failed to prepare the vector index.");
                throw;
            }
        }
    }
}
