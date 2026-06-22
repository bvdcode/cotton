// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Describes a archive download entry.
    /// </summary>
    public abstract record ArchiveDownloadEntry(string Path, long SizeBytes, bool IsDirectory) : IStoredZipEntry;
}
