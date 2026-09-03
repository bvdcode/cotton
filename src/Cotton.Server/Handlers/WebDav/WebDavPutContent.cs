// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Handlers.WebDav
{
    public record WebDavPutContent(
        List<Chunk> Chunks,
        byte[] FileHash,
        long TotalBytes);
}
