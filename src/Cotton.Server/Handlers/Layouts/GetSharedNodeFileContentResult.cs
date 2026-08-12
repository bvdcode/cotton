// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeFileContentResult(
        GetSharedNodeFileContentStatus Status,
        NodeFile? NodeFile = null,
        bool ServesPreview = false);
}
