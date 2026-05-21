// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class LatestDatabaseBackupDto
    {
        public string BackupId { get; set; } = null!;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime PointerUpdatedAtUtc { get; set; }
        public long DumpSizeBytes { get; set; }
        public int ChunkCount { get; set; }
        public string DumpContentHash { get; set; } = null!;
        public string SourceDatabase { get; set; } = null!;
        public string SourceHost { get; set; } = null!;
        public string SourcePort { get; set; } = null!;
    }
}
