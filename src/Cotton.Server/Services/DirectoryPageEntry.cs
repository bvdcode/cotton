// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class DirectoryPageEntry
    {
        public Guid Id { get; init; }
        public bool IsFolder { get; init; }
        public string NameKey { get; init; } = string.Empty;
        public long? SizeBytes { get; init; }
    }
}
