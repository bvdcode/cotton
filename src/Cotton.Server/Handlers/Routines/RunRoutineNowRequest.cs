// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Routines;
using Cotton.Server.Jobs;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Handlers.Routines
{
    public record RunRoutineNowRequest(Guid UserId, Guid RoutineId) : IRequest;

    public class RunRoutineNowRequestHandler(CottonDbContext db, ISchedulerFactory scheduler) : IRequestHandler<RunRoutineNowRequest>
    {
        public async Task Handle(RunRoutineNowRequest request, CancellationToken ct)
        {
            Routine routine = await db.Routines.SingleOrDefaultAsync(x => x.Id == request.RoutineId && x.OwnerId == request.UserId, ct)
                ?? throw new EntityNotFoundException<Routine>("Routine not found.");
            if (!routine.Enabled || await db.RoutineRuns.AnyAsync(x => x.RoutineId == routine.Id && x.Status == RoutineRunStatus.Running, ct))
            {
                throw new BadRequestException<Routine>("The routine is paused or already running.");
            }
            routine.LastExecutionAt = null;
            await db.SaveChangesAsync(ct);
            IScheduler instance = await scheduler.GetScheduler(ct);
            await instance.TriggerJob(new JobKey(nameof(RunRoutinesJob)),
                new JobDataMap { [RunRoutinesJob.RequestedRoutineKey] = routine.Id.ToString() }, ct);
        }
    }
}
