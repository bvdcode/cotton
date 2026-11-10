// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Routines;
using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Dto
{
    public class RoutineDraftDto
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [EnumDataType(typeof(RoutineType))]
        public RoutineType Type { get; set; }

        public bool Enabled { get; set; }

        public Guid TargetNodeId { get; set; }

        [Range(1, 8760)]
        public int IntervalHours { get; set; }

        public DateTime? StartAtLocal { get; set; }

        [EnumDataType(typeof(RoutineScheduleKind))]
        public RoutineScheduleKind ScheduleKind { get; set; }

        public RoutineCalendarSettings? Calendar { get; set; }

        [Required]
        public DeleteFilesRoutineSettings Settings { get; set; } = null!;
    }
}
