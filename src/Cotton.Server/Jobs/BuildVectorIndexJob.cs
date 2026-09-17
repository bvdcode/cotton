// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class BuildVectorIndexJob(
        IMediator mediator,
        ILogger<BuildVectorIndexJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                string? errorCode = await mediator.Send(new BuildVectorIndexRequest(), context.CancellationToken);
                if (errorCode is not null)
                {
                    logger.LogWarning("Vector index preparation did not complete: {ErrorCode}.", errorCode);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to prepare the vector index.");
                throw;
            }
        }
    }
}
