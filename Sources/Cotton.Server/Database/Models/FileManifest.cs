// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Cotton.Server.Database.Models
{
    [Table("file_manifests")]
    [Index(nameof(Sha256), IsUnique = true)]
    public class FileManifest : BaseEntity<Guid>
    {
        [Column("content_type")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("sha256")]
        public byte[] Sha256 { get; set; } = null!;

        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
