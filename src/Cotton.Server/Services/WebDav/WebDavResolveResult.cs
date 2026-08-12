// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav
{
    public record WebDavResolveResult
    {
        public bool Found { get; init; }

        public bool IsCollection { get; init; }

        public Node? Node { get; init; }

        public NodeFile? NodeFile { get; init; }
    }
}
