// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("server_settings")]
    public class CottonServerSettings : BaseEntity<Guid>
    {
        [Column("encryption_threads")]
        public int EncryptionThreads { get; init; }

        [Column("cipher_chunk_size_bytes")]
        public int CipherChunkSizeBytes { get; init; }

        [Column("max_chunk_size_bytes")]
        public int MaxChunkSizeBytes { get; init; }

        [Column("session_timeout_hours")]
        public int SessionTimeoutHours { get; init; } = 30 * 24;

        [Column("allow_cross_user_deduplication")]
        public bool AllowCrossUserDeduplication { get; init; }

        [Column("allow_global_indexing")]
        public bool AllowGlobalIndexing { get; init; }

        [Column("telemetry_enabled")]
        public bool TelemetryEnabled { get; init; }

        [Column("timezone")]
        public string Timezone { get; init; } = null!;
    }
}
