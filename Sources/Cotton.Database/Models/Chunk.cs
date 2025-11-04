// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("chunks")]
    public class Chunk
    {
        [Key]
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }
    }
}
