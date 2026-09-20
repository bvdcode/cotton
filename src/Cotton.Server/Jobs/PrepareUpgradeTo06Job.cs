// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 12)]
    public class PrepareUpgradeTo06Job(IMediator mediator, ILogger<PrepareUpgradeTo06Job> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            try
            {
                await mediator.Send(new PrepareUpgradeTo06Request(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to prepare the upgrade to Cotton {TargetVersion}", PrepareUpgradeTo06RequestHandler.TargetVersion);
                throw;
            }
        }
    }
}
