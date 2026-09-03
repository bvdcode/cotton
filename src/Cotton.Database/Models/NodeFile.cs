// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("node_files")]
    [Index(nameof(NodeId), nameof(NameKey), nameof(OwnerId), nameof(Id))]
    [Index(nameof(OwnerId), nameof(CreatedAt))]
    [Index(nameof(FileManifestId), nameof(NodeId))]
    public class NodeFile : BaseOwnedEntity<Guid>
    {
        [Column("file_manifest_id")]
        public Guid FileManifestId { get; set; }

        [Column("node_id")]
        public Guid NodeId { get; set; }

        [Column("original_node_file_id")]
        public Guid OriginalNodeFileId { get; set; }

        [Column("name")]
        public string Name { get; private set; } = null!;

        [Column("name_key", TypeName = "citext")]
        public string NameKey { get; private set; } = null!;

        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }

        public void SetName(string input)
        {
            bool isValid = NameValidator.TryNormalizeAndValidate(input, out string normalized, out string error);
            if (!isValid)
            {
                throw new ArgumentException($"Invalid node name: {error}");
            }
            Name = normalized;
            NameKey = NameValidator.GetNameKey(normalized);
        }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual FileManifest FileManifest { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Node Node { get; set; } = null!;

        public virtual ICollection<DownloadToken> DownloadTokens { get; set; } = [];
    }
}
