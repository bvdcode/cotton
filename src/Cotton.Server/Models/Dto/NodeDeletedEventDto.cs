// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public record NodeDeletedEventDto(
        Guid NodeId,
        Guid? ParentNodeId);
}
