// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("file_manifest_chunks")]
    [Index(nameof(ChunkHash))]
    [Index(nameof(FileManifestHash), nameof(ChunkOrder), IsUnique = true)]
    public class FileManifestChunk : BaseEntity<Guid>
    {
        [Column("chunk_order")]
        public int ChunkOrder { get; set; } // 0..N-1

        [Column("file_manifest_hash")]
        public byte[] FileManifestHash { get; set; } = null!;

        [Column("chunk_hash")]
        public byte[] ChunkHash { get; set; } = null!;

        public virtual Chunk Chunk { get; set; } = null!;
        public virtual FileManifest FileManifest { get; set; } = null!;
    }
}
