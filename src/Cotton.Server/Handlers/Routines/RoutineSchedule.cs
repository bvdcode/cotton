// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Routines
{
    public static class RoutineSchedule
    {
        public static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(1);
        public static readonly TimeSpan MaximumInterval = TimeSpan.FromDays(365);

        public static bool IsDue(DateTime? lastExecutionAt, TimeSpan interval, DateTime now)
        {
            return !lastExecutionAt.HasValue || now - lastExecutionAt.Value >= interval;
        }
    }
}
