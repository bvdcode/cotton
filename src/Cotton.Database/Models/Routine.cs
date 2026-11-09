// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Routines;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("routines")]
    [Index(nameof(OwnerId))]
    public class Routine : BaseOwnedEntity<Guid>
    {
        [Column("name"), MaxLength(200)]
        public string Name { get; set; } = null!;

        [Column("type")]
        public RoutineType Type { get; set; }

        [Column("enabled")]
        public bool Enabled { get; set; }

        [Column("target_node_id")]
        public Guid TargetNodeId { get; set; }

        [Column("interval")]
        public TimeSpan Interval { get; set; }

        [Column("start_at")]
        public DateTime? StartAt { get; set; }

        [Column("schedule_kind")]
        public RoutineScheduleKind ScheduleKind { get; set; }

        [Column("calendar_settings", TypeName = "jsonb")]
        public string? CalendarSettings { get; set; }

        [Column("last_execution_at")]
        public DateTime? LastExecutionAt { get; set; }

        [Column("settings", TypeName = "jsonb")]
        public string Settings { get; set; } = null!;
    }
}
