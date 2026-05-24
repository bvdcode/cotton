// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents one deduplicated encrypted storage chunk.</summary>
    [Table("chunks")]
    [Index(nameof(GCScheduledAfter))]
    public class Chunk
    {
        /// <summary>Content hash that identifies the stored chunk.</summary>
        [Key]
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;

        /// <summary>
        /// Plain size of the chunk in bytes before transformations like compression or encryption.
        /// </summary>
        [Column("plain_size_bytes")]
        public long PlainSizeBytes { get; set; }

        /// <summary>Size of the chunk bytes stored in the backend after pipeline processing.</summary>
        [Column("stored_size_bytes")]
        public long StoredSizeBytes { get; set; }

        /// <summary>UTC time after which an unreferenced chunk may be garbage-collected.</summary>
        [Column("gc_scheduled_after")]
        public DateTime? GCScheduledAfter { get; set; }

        /// <summary>Compression algorithm used for this stored chunk.</summary>
        [Column("compression_algorithm")]
        public CompressionAlgorithm CompressionAlgorithm { get; set; }

        /// <summary>Chunk ownership rows used for proof-of-ownership checks.</summary>
        public virtual ICollection<ChunkOwnership> ChunkOwnerships { get; set; } = [];
        /// <summary>Ordered manifest-to-chunk mapping rows.</summary>
        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
