// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal node projection for existing-data startup probes.
    /// </summary>
    [Table("nodes")]
    internal class MasterKeyProbeNode
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
    }
}
