// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the latest database backup API payload.
    /// </summary>
    public class LatestDatabaseBackupDto
    {
        /// <summary>
        /// Gets or sets backup id.
        /// </summary>
        public string BackupId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the UTC timestamp when the resource was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets pointer updated at utc.
        /// </summary>
        public DateTime PointerUpdatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets dump size bytes.
        /// </summary>
        public long DumpSizeBytes { get; set; }
        /// <summary>
        /// Gets or sets chunk count.
        /// </summary>
        public int ChunkCount { get; set; }
        /// <summary>
        /// Gets or sets dump content hash.
        /// </summary>
        public string DumpContentHash { get; set; } = null!;
        /// <summary>
        /// Gets or sets source database.
        /// </summary>
        public string SourceDatabase { get; set; } = null!;
        /// <summary>
        /// Gets or sets source host.
        /// </summary>
        public string SourceHost { get; set; } = null!;
        /// <summary>
        /// Gets or sets source port.
        /// </summary>
        public string SourcePort { get; set; } = null!;
    }
}
