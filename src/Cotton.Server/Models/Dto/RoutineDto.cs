// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Routines;
using Cotton.Nodes;
using Cotton.Server.Handlers.Routines;
using System.Globalization;
using EasyExtensions.Models.Dto;
using System.Text.Json;

namespace Cotton.Server.Models.Dto
{
    public class RoutineDto : BaseDto<Guid>
    {
        public string Name { get; set; } = null!;
        public RoutineType Type { get; set; }
        public bool Enabled { get; set; }
        public Guid TargetNodeId { get; set; }
        public NodeDto? TargetNode { get; set; }
        public int IntervalHours { get; set; }
        public DateTime? LastExecutionAt { get; set; }
        public DateTime? NextExecutionAt { get; set; }
        public DeleteFilesRoutineSettings Settings { get; set; } = null!;
        public string? StartAtLocal { get; set; }
        public RoutineScheduleKind ScheduleKind { get; set; }
        public RoutineCalendarSettings? Calendar { get; set; }
        public string Timezone { get; set; } = null!;
        public RoutineRunDto? LatestRun { get; set; }

        public static RoutineDto FromEntity(Routine routine, TimeZoneInfo timezone, RoutineRun? latestRun = null) => new()
        {
            Id = routine.Id, CreatedAt = routine.CreatedAt, UpdatedAt = routine.UpdatedAt,
            Name = routine.Name, Type = routine.Type, Enabled = routine.Enabled,
            TargetNodeId = routine.TargetNodeId, IntervalHours = (int)routine.Interval.TotalHours,
            LastExecutionAt = routine.LastExecutionAt,
            NextExecutionAt = RoutineSchedule.NextExecution(routine, timezone),
            StartAtLocal = routine.StartAt.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(routine.StartAt.Value, timezone).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : null,
            ScheduleKind = routine.ScheduleKind, Timezone = timezone.Id,
            Calendar = routine.CalendarSettings is null ? null : JsonSerializer.Deserialize<RoutineCalendarSettings>(routine.CalendarSettings),
            Settings = JsonSerializer.Deserialize<DeleteFilesRoutineSettings>(routine.Settings)
                ?? throw new InvalidOperationException("Routine settings are required."),
            LatestRun = latestRun is null ? null : RoutineRunDto.FromEntity(latestRun),
        };
    }
}
