// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("file_manifest_chunks")]
    [Index(nameof(ChunkSha256))]
    [Index(nameof(FileManifestId), nameof(ChunkOrder), IsUnique = true)]
    public class FileManifestChunk : BaseEntity<Guid>
    {
        [Column("chunk_order")]
        public int ChunkOrder { get; set; } // 0..N-1

        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("chunk_sha256")]
        public byte[] ChunkSha256 { get; set; } = null!;

        public virtual Chunk Chunk { get; set; } = null!;
        public virtual FileManifest FileManifest { get; set; } = null!;
    }
}
