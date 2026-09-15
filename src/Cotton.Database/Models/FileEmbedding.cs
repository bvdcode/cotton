// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("file_embeddings")]
    [Index(nameof(FileManifestId), nameof(IndexVersion), nameof(FragmentIndex), IsUnique = true)]
    public class FileEmbedding : BaseEntity<Guid>
    {
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("fragment_index")]
        public int FragmentIndex { get; set; }

        [Column("index_version")]
        public int IndexVersion { get; set; }

        [Column("embedding", TypeName = "real[]")]
        public float[] Embedding { get; set; } = [];

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual FileManifest FileManifest { get; set; } = null!;
    }
}
