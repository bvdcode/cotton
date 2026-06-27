// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal chunk projection for master-key startup probes.
    /// </summary>
    [Table("chunks")]
    internal class MasterKeyProbeChunk
    {
        /// <summary>
        /// Storage hash.
        /// </summary>
        [Key]
        [Column("hash")]
        public byte[] Hash { get; set; } = null!;
    }
}
