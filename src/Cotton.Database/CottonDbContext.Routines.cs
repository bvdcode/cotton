// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Database
{
    public partial class CottonDbContext
    {
        public DbSet<Routine> Routines => Set<Routine>();

        public DbSet<RoutineRun> RoutineRuns => Set<RoutineRun>();
    }
}
