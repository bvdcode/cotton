// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Replicated encrypted keyring system object.</summary>
    [Table("cotton_keyring_objects")]
    public class KeyringObject
    {
        /// <summary>Canonical keyring object name, such as a v2 immutable object or head marker.</summary>
        [Key]
        [Column("name")]
        [MaxLength(512)]
        public string Name { get; set; } = null!;

        /// <summary>Canonical object bytes as written by the keyring object store.</summary>
        [Column("bytes")]
        public byte[] Bytes { get; set; } = null!;

        /// <summary>UTC timestamp when this replica row was first created.</summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC timestamp when this replica row was last updated.</summary>
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
