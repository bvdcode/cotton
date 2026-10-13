// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public record TrashParent(Guid Id, string Name, Dictionary<string, string>? Metadata);
}
