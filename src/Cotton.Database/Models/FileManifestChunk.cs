// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Maps a file manifest to its ordered storage chunks.
    /// </summary>
    [Table("file_manifest_chunks")]
    [Index(nameof(ChunkHash))]
    [Index(nameof(FileManifestId), nameof(ChunkOrder), IsUnique = true)]
    public class FileManifestChunk : BaseEntity<Guid>
    {
        /// <summary>
        /// Identifier of the immutable file manifest referenced by this row.
        /// </summary>
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        /// <summary>
        /// Zero-based position of this chunk within the manifest's ordered chunk sequence.
        /// </summary>
        [Column("chunk_order")]
        public int ChunkOrder { get; set; } // 0..N-1

        /// <summary>
        /// Hash of the chunk referenced by this row.
        /// </summary>
        [Column("chunk_hash")]
        public byte[] ChunkHash { get; set; } = null!;

        /// <summary>
        /// Navigation property for the referenced chunk.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Chunk Chunk { get; set; } = null!;

        /// <summary>
        /// Navigation property for immutable file content.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual FileManifest FileManifest { get; set; } = null!;
    }
}
