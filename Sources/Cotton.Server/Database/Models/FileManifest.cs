// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Database.Models
{
    [Table("file_manifests")]
    public class FileManifest
    {
        [Key]
        [Column("sha256")]
        public byte[] Sha256 { get; set; } = null!;

        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
