// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Routines;

namespace Cotton.Server.Models
{
    public record FileDeletionCondition(Guid RootNodeId, bool IncludeSubfolders, RoutineDateField DateField, DateTime OlderThan);
}
