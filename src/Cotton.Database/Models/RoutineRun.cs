// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Routines;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("routine_runs")]
    [Index(nameof(RoutineId), nameof(CreatedAt))]
    public class RoutineRun : BaseEntity<Guid>
    {
        [Column("routine_id")]
        public Guid RoutineId { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Routine Routine { get; set; } = null!;

        [Column("status")]
        public RoutineRunStatus Status { get; set; }

        [Column("finished_at")]
        public DateTime? FinishedAt { get; set; }

        [Column("processed_count")]
        public int ProcessedCount { get; set; }

        [Column("deleted_count")]
        public int DeletedCount { get; set; }

        [Column("error"), MaxLength(2048)]
        public string? Error { get; set; }
    }
}
