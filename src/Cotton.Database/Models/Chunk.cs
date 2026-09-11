// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("chunks")]
    [Index(nameof(GCScheduledAfter))]
    [Index(nameof(Hash), nameof(GCScheduledAfter))]
    public class Chunk
    {
        [Key]
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;

        [Column("plain_size_bytes")]
        public long PlainSizeBytes { get; set; }

        [Column("stored_size_bytes")]
        public long StoredSizeBytes { get; set; }

        [Column("gc_scheduled_after")]
        public DateTime? GCScheduledAfter { get; set; }

        [Column("compression_algorithm")]
        public CompressionAlgorithm CompressionAlgorithm { get; set; }

        /// <summary>
        /// Chunk ownership rows used for proof-of-ownership checks.
        /// </summary>
        public virtual ICollection<ChunkOwnership> ChunkOwnerships { get; set; } = [];

        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
