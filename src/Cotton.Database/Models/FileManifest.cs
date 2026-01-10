// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("file_manifests")]
    [Index(nameof(ProposedContentHash), IsUnique = true)]
    [Index(nameof(ComputedContentHash), IsUnique = true)]
    public class FileManifest : BaseEntity<Guid>
    {
        [Column("computed_content_hash")]
        public byte[]? ComputedContentHash { get; set; }

        [Column("proposed_content_hash")]
        public byte[] ProposedContentHash { get; set; } = null!;

        [Column("content_type", TypeName = "citext")]
        public string ContentType { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("encrypted_file_preview_hash")]
        public byte[]? EncryptedFilePreviewHash { get; set; }

        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
