// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Shared.Contracts.Files;

/// <summary>
/// Represents one file version entry in a Cotton file lineage.
/// </summary>
public sealed class FileVersionDto
{
    /// <summary>
    /// Gets or sets the version row identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the current visible node file identifier.
    /// </summary>
    public Guid NodeFileId { get; set; }

    /// <summary>
    /// Gets or sets the immutable file manifest identifier.
    /// </summary>
    public Guid FileManifestId { get; set; }

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
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the one-based version number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the current visible version.
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the original version.
    /// </summary>
    public bool IsOriginal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this version can be deleted.
    /// </summary>
    public bool CanDelete { get; set; }
}
