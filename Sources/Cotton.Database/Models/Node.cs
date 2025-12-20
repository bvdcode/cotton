// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Database.Abstractions;
using Cotton.Database.Models.Enums;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("nodes")]
    [Index(nameof(LayoutId), nameof(ParentId), nameof(Type), nameof(NameKey), IsUnique = true)]
    public class Node : BaseOwnedEntity
    {
        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [Column("type")]
        // TODO: make sure the parent node type is the same as this node type
        public NodeType Type { get; set; }

        [Column("name")]
        public string Name { get; private set; } = null!;

        [Column("name_key")]
        public string NameKey { get; private set; } = null!;

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

        public virtual Layout Layout { get; set; } = null!;
        public virtual Node? Parent { get; set; }
        public virtual ICollection<Node> Children { get; set; } = [];
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
    }
}
