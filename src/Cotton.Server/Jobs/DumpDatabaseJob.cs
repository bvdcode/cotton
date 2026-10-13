// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 7)]
    public class DumpDatabaseJob(IMediator mediator) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await JobStartupDelays.WaitForDumpDatabaseAsync(context.CancellationToken);
            await mediator.Send(new CreateDatabaseBackupRequest(), context.CancellationToken);
        }
    }
}
