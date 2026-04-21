// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("chunks")]
    public class Chunk
    {
        [Key]
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;

        /// <summary>
        /// Plain size of the chunk in bytes before transformations like compression or encryption.
        /// </summary>
        [Column("plain_size_bytes")]
        public long PlainSizeBytes { get; set; }

        [Column("stored_size_bytes")]
        public long StoredSizeBytes { get; set; }

        [Column("gc_scheduled_after")]
        public DateTime? GCScheduledAfter { get; set; }

        [Column("compression_algorithm")]
        public CompressionAlgorithm CompressionAlgorithm { get; set; }

        public virtual ICollection<ChunkOwnership> ChunkOwnerships { get; set; } = [];
        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
