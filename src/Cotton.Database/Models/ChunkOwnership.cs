// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Links a user to a chunk they are allowed to reference during proof-of-ownership checks.</summary>
    [Table("chunk_ownerships")]
    [Index(nameof(OwnerId), nameof(ChunkHash), IsUnique = true)]
    public class ChunkOwnership : BaseOwnedEntity<Guid>
    {
        /// <summary>Hash of the chunk referenced by this row.</summary>
        [Column("chunk_hash")]
        public byte[] ChunkHash { get; set; } = null!;

        /// <summary>Navigation property for the referenced chunk.</summary>
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Chunk Chunk { get; set; } = null!;
    }
}
