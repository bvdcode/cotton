// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Routines;
using Quartz;
using System.Globalization;
using System.Text.Json;

namespace Cotton.Server.Handlers.Routines
{
    public static class RoutineSchedule
    {
        public static bool IsDue(DateTime? lastExecutionAt, TimeSpan interval, DateTime now)
            => !lastExecutionAt.HasValue || now - lastExecutionAt.Value >= interval;

        public static DateTime? NextExecution(Routine routine, TimeZoneInfo timezone)
        {
            if (!routine.Enabled)
            {
                return null;
            }
            if (!routine.LastExecutionAt.HasValue)
            {
                return routine.StartAt ?? routine.CreatedAt;
            }
            DateTime? next = routine.ScheduleKind switch
            {
                RoutineScheduleKind.Interval => routine.LastExecutionAt.Value + routine.Interval,
                RoutineScheduleKind.Calendar => NextCalendar(routine.LastExecutionAt.Value,
                    JsonSerializer.Deserialize<RoutineCalendarSettings>(routine.CalendarSettings!)
                        ?? throw new InvalidOperationException("Calendar settings are required."), timezone),
                _ => throw new InvalidOperationException("Unsupported routine schedule."),
            };
            return next.HasValue && routine.StartAt > next ? routine.StartAt : next;
        }

        public static DateTime? NextCalendar(DateTime lastExecution, RoutineCalendarSettings settings, TimeZoneInfo timezone)
        {
            string time = FormattableString.Invariant($"0 {settings.Minute} {settings.Hour}");
            string expression = settings.Frequency switch
            {
                RoutineCalendarFrequency.Daily => $"{time} * * ?",
                RoutineCalendarFrequency.Weekly => $"{time} ? * {(settings.DayOfWeek + 1).ToString(CultureInfo.InvariantCulture)}",
                RoutineCalendarFrequency.Monthly => $"{time} {settings.DayOfMonth.ToString(CultureInfo.InvariantCulture)} * ?",
                RoutineCalendarFrequency.Yearly => $"{time} {settings.DayOfMonth.ToString(CultureInfo.InvariantCulture)} {settings.Month.ToString(CultureInfo.InvariantCulture)} ?",
                _ => throw new ArgumentOutOfRangeException(nameof(settings)),
            };
            CronExpression cron = new(expression) { TimeZone = timezone };
            DateTime localLast = TimeZoneInfo.ConvertTimeFromUtc(lastExecution, timezone);
            DateTimeOffset? next = cron.GetNextValidTimeAfter(new DateTimeOffset(lastExecution));
            while (next.HasValue)
            {
                DateTime localNext = TimeZoneInfo.ConvertTimeFromUtc(next.Value.UtcDateTime, timezone);
                if (localNext > localLast && localNext.Hour == settings.Hour && localNext.Minute == settings.Minute)
                {
                    break;
                }
                next = cron.GetNextValidTimeAfter(next.Value);
            }
            return next?.UtcDateTime;
        }
    }
}
