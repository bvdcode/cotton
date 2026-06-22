// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Provides a ZIP entry path, fixed uncompressed length, and deferred stream opener.
    /// </summary>
    public record StoredZipSourceEntry(
        string Path,
        long SizeBytes,
        bool IsDirectory,
        Func<CancellationToken, ValueTask<Stream>> OpenReadAsync) : IStoredZipEntry;
}
