// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 12)]
    public class HotfixBackfillContentTypeJob(IMediator mediator, ILogger<HotfixBackfillContentTypeJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            try
            {
                await mediator.Send(new PrepareContentTypeUpgradeRequest(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to migrate file content types");
                throw;
            }
        }
    }
}
