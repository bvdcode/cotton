// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Server.Services
{
    [JsonDerivedType(typeof(ArchiveDownloadDirectoryEntry), "directory")]
    [JsonDerivedType(typeof(ArchiveDownloadFileEntry), "file")]
    public abstract record ArchiveDownloadEntry(string Path, long SizeBytes, bool IsDirectory) : IStoredZipEntry;
}
