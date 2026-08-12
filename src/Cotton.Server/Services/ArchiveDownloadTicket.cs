// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    public record ArchiveDownloadTicket(
        string FileName,
        long SizeBytes,
        int EntryCount,
        IReadOnlyList<ArchiveDownloadEntry> Entries);
}
