// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
        public NodeType Type { get; set; }

        [Column("name")]
        public string Name { get; private set; } = null!;

        [Column("name_key", TypeName = "citext")]
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

        public void SetParent(Node parent)
        {
            EnsureParentMatches(parent, Type);
            ParentId = parent.Id;
        }

        public void SetParent(Node parent, NodeType nodeType)
        {
            EnsureParentMatches(parent, nodeType);
            Type = nodeType;
            ParentId = parent.Id;
        }

        private void EnsureParentMatches(Node parent, NodeType nodeType)
        {
            if (parent.OwnerId != OwnerId)
            {
                throw new InvalidOperationException("Parent node belongs to another owner.");
            }

            if (parent.LayoutId != LayoutId)
            {
                throw new InvalidOperationException("Parent node belongs to another layout.");
            }

            if (parent.Type != nodeType)
            {
                throw new InvalidOperationException("Parent node type must match child node type.");
            }
        }

        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; } = [];

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Layout Layout { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Node? Parent { get; set; }

        public virtual ICollection<Node> Children { get; set; } = [];
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
    }
}
