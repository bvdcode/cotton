// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav
{
    public record WebDavParentResult
    {
        public bool Found { get; init; }

        public Node? ParentNode { get; init; }

        public string? ResourceName { get; init; }
    }
}
