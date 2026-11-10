// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Routines;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Routines
{
    public record RunDueRoutinesRequest(Guid? RequestedRoutineId = null) : IRequest;

    public class RunDueRoutinesRequestHandler(CottonDbContext db, IServiceScopeFactory scopes,
        SettingsProvider settings, IMediator mediator, ILogger<RunDueRoutinesRequestHandler> logger)
        : IRequestHandler<RunDueRoutinesRequest>
    {
        public async Task Handle(RunDueRoutinesRequest request, CancellationToken ct)
        {
            List<RoutineRun> interrupted = await db.RoutineRuns.Include(x => x.Routine)
                .Where(x => x.Status == RoutineRunStatus.Running).ToListAsync(ct);
            foreach (RoutineRun run in interrupted)
            {
                run.Status = RoutineRunStatus.Interrupted;
                run.FinishedAt = DateTime.UtcNow;
                run.Error = "Interrupted";
                logger.LogWarning("Routine {RoutineId} execution {RunId} was interrupted.", run.RoutineId, run.Id);
            }
            await db.SaveChangesAsync(ct);
            foreach (RoutineRun run in interrupted)
            {
                await mediator.Send(new PublishRoutineRunRequest(run.Routine.OwnerId, RoutineRunDto.FromEntity(run)), ct);
            }
            DateTime now = DateTime.UtcNow;
            TimeZoneInfo timezone = settings.GetServerSettings().GetTimezoneInfo();
            List<Routine> routines = await db.Routines.AsNoTracking().Where(x => x.Enabled).ToListAsync(ct);
            foreach (Routine routine in routines)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    bool force = request.RequestedRoutineId == routine.Id && !routine.LastExecutionAt.HasValue;
                    DateTime? next = RoutineSchedule.NextExecution(routine, timezone);
                    if (!force && (!next.HasValue || next > now))
                    {
                        continue;
                    }
                    await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                    IMediator executor = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await executor.Send(new ExecuteRoutineRequest(routine.Id, force), ct);
                }
                catch (Exception exception) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(exception, "Could not execute routine {RoutineId}.", routine.Id);
                }
            }
        }

    }
}
