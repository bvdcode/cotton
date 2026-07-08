// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal file-manifest projection for master-key startup probes.
    /// </summary>
    [Table("file_manifests")]
    internal class MasterKeyProbeFileManifest
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Encrypted small-preview storage hash bytes.
        /// </summary>
        [Column("small_file_preview_hash_encrypted")]
        public byte[]? SmallFilePreviewHashEncrypted { get; set; }
    }
}
