// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Cotton.Routines
{
    public class RoutineCalendarSettings
    {
        [EnumDataType(typeof(RoutineCalendarFrequency))]
        public RoutineCalendarFrequency Frequency { get; set; }

        [Range(0, 23)]
        public int Hour { get; set; }

        [Range(0, 59)]
        public int Minute { get; set; }

        [Range(0, 6)]
        public int DayOfWeek { get; set; }

        [Range(1, 31)]
        public int DayOfMonth { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }
    }
}
