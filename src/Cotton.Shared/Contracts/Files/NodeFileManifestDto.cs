// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Common;

namespace Cotton.Contracts.Files;

/// <summary>
/// Represents a visible Cotton file entry and its immutable content manifest metadata.
/// </summary>
public sealed class NodeFileManifestDto : BaseApiDto
{
    /// <summary>
    /// Gets or sets the parent node identifier.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Gets or sets the immutable file manifest identifier.
    /// </summary>
    public Guid FileManifestId { get; set; }

    /// <summary>
    /// Gets or sets the original file identifier shared by all versions in the lineage.
    /// </summary>
    public Guid OriginalNodeFileId { get; set; }

    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the lowercase hexadecimal full-content hash.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the strong content ETag without quotes.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets structured metadata attached to the file entry.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether server-side video transcoding is required for playback.
    /// </summary>
    public bool RequiresVideoTranscoding { get; set; }

    /// <summary>
    /// Gets or sets the encrypted preview hash token.
    /// </summary>
    public string? PreviewHashEncryptedHex { get; set; }
}
