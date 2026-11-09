// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Routines;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(minutes: 15)]
    public class RunRoutinesJob(IMediator mediator) : IJob
    {
        public const string RequestedRoutineKey = "RequestedRoutineId";

        public async Task Execute(IJobExecutionContext context)
        {
            string? requested = context.MergedJobDataMap.GetString(RequestedRoutineKey);
            Guid? routineId = Guid.TryParse(requested, out Guid id) ? id : null;
            await mediator.Send(new RunDueRoutinesRequest(routineId), context.CancellationToken);
        }
    }
}
