// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Routines;
using Cotton.Server.Jobs;
using Cotton.Server.Providers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Cotton.Server.Handlers.Routines
{
    public record SaveRoutineRequest(Guid UserId, Guid? RoutineId, RoutineDraftDto Draft) : IRequest<RoutineDto>;

    public class SaveRoutineRequestHandler(CottonDbContext db, ISchedulerFactory scheduler, SettingsProvider settings,
        CommittedMutationEffects effects)
        : IRequestHandler<SaveRoutineRequest, RoutineDto>
    {
        public async Task<RoutineDto> Handle(SaveRoutineRequest request, CancellationToken ct)
        {
            RoutineDraftDto draft = request.Draft;
            Validator.ValidateObject(draft, new ValidationContext(draft), true);
            Validator.ValidateObject(draft.Settings, new ValidationContext(draft.Settings), true);
            string name = draft.Name.Trim();
            if (name.Length == 0 || draft.Type != RoutineType.DeleteFiles)
            {
                throw new BadRequestException<Routine>("Invalid routine definition.");
            }
            if (!await db.Nodes.AnyAsync(x => x.Id == draft.TargetNodeId && x.OwnerId == request.UserId
                && x.Type == NodeType.Default && x.Layout.IsActive, ct))
            {
                throw new EntityNotFoundException<Node>("Working folder not found.");
            }
            TimeZoneInfo timezone = settings.GetServerSettings().GetTimezoneInfo();
            if (draft.ScheduleKind == RoutineScheduleKind.Calendar)
            {
                if (draft.Calendar is null)
                {
                    throw new BadRequestException<Routine>("Calendar settings are required.");
                }
                Validator.ValidateObject(draft.Calendar, new ValidationContext(draft.Calendar), true);
                if (!RoutineSchedule.NextCalendar(DateTime.UtcNow, draft.Calendar, timezone).HasValue)
                {
                    throw new BadRequestException<Routine>("The calendar schedule has no valid dates.");
                }
            }
            DateTime? startAt = null;
            if (draft.StartAtLocal.HasValue)
            {
                DateTime local = DateTime.SpecifyKind(draft.StartAtLocal.Value, DateTimeKind.Unspecified);
                if (timezone.IsInvalidTime(local))
                {
                    throw new BadRequestException<Routine>("The start time does not exist in the server timezone.");
                }
                startAt = TimeZoneInfo.ConvertTimeToUtc(local, timezone);
            }
            Routine routine;
            if (request.RoutineId.HasValue)
            {
                routine = await db.Routines.SingleOrDefaultAsync(x => x.Id == request.RoutineId && x.OwnerId == request.UserId, ct)
                    ?? throw new EntityNotFoundException<Routine>("Routine not found.");
                if (await db.RoutineRuns.AnyAsync(x => x.RoutineId == routine.Id && x.Status == RoutineRunStatus.Running, ct))
                {
                    throw new BadRequestException<Routine>("The routine is running.");
                }
            }
            else
            {
                routine = new Routine { OwnerId = request.UserId };
                db.Routines.Add(routine);
            }
            routine.Name = name;
            routine.Type = draft.Type;
            routine.Enabled = draft.Enabled;
            routine.TargetNodeId = draft.TargetNodeId;
            routine.Interval = TimeSpan.FromHours(draft.IntervalHours);
            routine.Settings = JsonSerializer.Serialize(draft.Settings);
            routine.ScheduleKind = draft.ScheduleKind;
            routine.CalendarSettings = draft.ScheduleKind switch
            {
                RoutineScheduleKind.Interval => null,
                RoutineScheduleKind.Calendar => JsonSerializer.Serialize(draft.Calendar),
                _ => throw new BadRequestException<Routine>("Unsupported routine schedule."),
            };
            routine.StartAt = startAt;
            await db.SaveChangesAsync(ct);
            if (!request.RoutineId.HasValue)
            {
                await effects.RunAsync(nameof(RunRoutinesJob), () => scheduler.TriggerJobAsync<RunRoutinesJob>());
            }
            return RoutineDto.FromEntity(routine, timezone);
        }
    }
}
