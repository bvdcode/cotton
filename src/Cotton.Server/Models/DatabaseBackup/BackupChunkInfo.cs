// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.DatabaseBackup
{
    /// <summary>
    /// Describes backup chunk information.
    /// </summary>
    public record BackupChunkInfo(int Order, string StorageKey, int SizeBytes);
}
