// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(seconds: 1, startNow: true, repeatForever: false)]
    public class HotfixBackfillContentTypeJob(IMediator mediator, ILogger<HotfixBackfillContentTypeJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            try
            {
                int updated = await mediator.Send(new BackfillNodeFileContentTypesRequest(), cancellationToken);
                logger.LogInformation("Backfilled content types for {Count} node files", updated);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to backfill node file content types");
                throw;
            }
        }
    }
}
