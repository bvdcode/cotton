// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Cotton.Database.Models.Enums;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>
    /// Represents a folder-like node inside one layout tree.
    /// </summary>
    [Table("nodes")]
    [Index(nameof(LayoutId), nameof(ParentId), nameof(Type), nameof(NameKey), IsUnique = true)]
    public class Node : BaseOwnedEntity<Guid>
    {
        /// <summary>
        /// Identifier of the layout tree that contains this node.
        /// </summary>
        [Column("layout_id")]
        public Guid LayoutId { get; set; }

        /// <summary>
        /// Identifier of the parent node, or null for a layout root.
        /// </summary>
        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        /// <summary>
        /// Domain type discriminator for this row.
        /// </summary>
        [Column("type")]
        public NodeType Type { get; set; }

        /// <summary>
        /// Human-readable name displayed to users.
        /// </summary>
        [Column("name")]
        public string Name { get; private set; } = null!;

        /// <summary>
        /// Normalized lookup key derived from the display name.
        /// </summary>
        [Column("name_key", TypeName = "citext")]
        public string NameKey { get; private set; } = null!;

        /// <summary>
        /// Validates and assigns the display name and lookup key together.
        /// </summary>
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

        /// <summary>
        /// Moves this node under a validated parent node.
        /// </summary>
        public void SetParent(Node parent)
        {
            EnsureParentMatches(parent, Type);
            ParentId = parent.Id;
        }

        /// <summary>
        /// Moves this node under a validated parent node.
        /// </summary>
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

        /// <summary>
        /// Extensible metadata associated with this row.
        /// </summary>
        [Column("metadata")]
        public Dictionary<string, string>? Metadata { get; set; } = [];

        /// <summary>
        /// Navigation property for the containing layout.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Layout Layout { get; set; } = null!;

        /// <summary>
        /// Navigation property for the parent node.
        /// </summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Node? Parent { get; set; }

        /// <summary>
        /// Child nodes contained by this node.
        /// </summary>
        public virtual ICollection<Node> Children { get; set; } = [];

        /// <summary>
        /// Visible file entries stored by the server.
        /// </summary>
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
    }
}
