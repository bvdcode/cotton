// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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

        [Column("instance_id")]
        public Guid InstanceId { get; init; }

        [Column("smtp_server_address")]
        public string? SmtpServerAddress { get; init; }

        [Column("smtp_server_port")]
        public int? SmtpServerPort { get; init; }

        [Column("smtp_username")]
        public string? SmtpUsername { get; init; }

        [Column("smtp_password_encrypted")]
        public string? SmtpPasswordEncrypted { get; init; }
    }
}
