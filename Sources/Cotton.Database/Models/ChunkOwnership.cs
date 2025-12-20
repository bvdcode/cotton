// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Database.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("chunk_ownerships")]
    [Index(nameof(OwnerId), nameof(ChunkHash), IsUnique = true)]
    public class ChunkOwnership : BaseOwnedEntity
    {
        [Column("chunk_hash")]
        public byte[] ChunkHash { get; set; } = null!;

        public virtual Chunk Chunk { get; set; } = null!;
    }
}
