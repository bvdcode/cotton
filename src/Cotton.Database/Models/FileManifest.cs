// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Represents immutable file content shared by one or more visible file entries.</summary>
    [Table("file_manifests")]
    [Index(nameof(ProposedContentHash), IsUnique = true)]
    [Index(nameof(ComputedContentHash), IsUnique = true)]
    [Index(nameof(SmallFilePreviewHash))]
    [Index(nameof(LargeFilePreviewHash))]
    public class FileManifest : BaseEntity<Guid>
    {
        /// <summary>Server-computed content hash used to verify uploads.</summary>
        [Column("computed_content_hash")]
        public byte[]? ComputedContentHash { get; set; }

        /// <summary>Client-proposed content hash used for deduplication and verification.</summary>
        [Column("proposed_content_hash")]
        public byte[] ProposedContentHash { get; set; } = null!;

        /// <summary>MIME content type associated with the file content.</summary>
        [Column("content_type", TypeName = "citext")]
        public string ContentType { get; set; } = null!;

        /// <summary>File content size in bytes.</summary>
        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        /// <summary>Encrypted storage hash for the small preview when stored privately.</summary>
        [Column("small_file_preview_hash_encrypted")]
        public byte[]? SmallFilePreviewHashEncrypted { get; set; }

        /// <summary>Plain storage hash for the small preview when it may be served directly.</summary>
        [Column("small_file_preview_hash")]
        public byte[]? SmallFilePreviewHash { get; set; }

        /// <summary>Storage hash for the large preview image.</summary>
        [Column("large_file_preview_hash")]
        public byte[]? LargeFilePreviewHash { get; set; }

        /// <summary>Latest preview generation error message, if preview generation failed.</summary>
        [Column("preview_generation_error")]
        public string? PreviewGenerationError { get; set; }

        /// <summary>Preview generator version used for the current previews.</summary>
        [Column("preview_generator_version")]
        public int PreviewGeneratorVersion { get; set; } = 0;

        /// <summary>Returns the encrypted small-preview hash as lowercase hexadecimal.</summary>
        public string? GetPreviewHashEncryptedHex()
        {
            if (SmallFilePreviewHashEncrypted is null)
            {
                return null;
            }
            return Convert.ToHexStringLower(SmallFilePreviewHashEncrypted);
        }

        /// <summary>Visible file entries stored by the server.</summary>
        public virtual ICollection<NodeFile> NodeFiles { get; set; } = [];
        /// <summary>Ordered manifest-to-chunk mapping rows.</summary>
        public virtual ICollection<FileManifestChunk> FileManifestChunks { get; set; } = [];
    }
}
