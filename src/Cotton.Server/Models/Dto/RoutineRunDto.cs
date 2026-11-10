// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Routines;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class RoutineRunDto : BaseDto<Guid>
    {
        public Guid RoutineId { get; set; }
        public RoutineRunStatus Status { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int ProcessedCount { get; set; }
        public int DeletedCount { get; set; }
        public string? Error { get; set; }

        public static RoutineRunDto FromEntity(RoutineRun run) => new()
        {
            Id = run.Id, CreatedAt = run.CreatedAt, UpdatedAt = run.UpdatedAt,
            RoutineId = run.RoutineId, Status = run.Status, FinishedAt = run.FinishedAt,
            ProcessedCount = run.ProcessedCount, DeletedCount = run.DeletedCount, Error = run.Error,
        };
    }
}
