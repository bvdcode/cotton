// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 12)]
    public class HotfixClearEmptyFilePreviewJob(
        IMediator mediator,
        ILogger<HotfixClearEmptyFilePreviewJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            try
            {
                int updated = await mediator.Send(new ClearEmptyFilePreviewRequest(), cancellationToken);
                if (updated > 0)
                {
                    logger.LogInformation("Cleared preview data for the empty file manifest");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to clear preview data for the empty file manifest");
                throw;
            }
        }
    }
}
