// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents the file version API payload.
/// </summary>
public sealed class FileVersionDto
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Gets or sets node file id.
    /// </summary>
    public Guid NodeFileId { get; init; }
    /// <summary>
    /// Gets or sets file manifest id.
    /// </summary>
    public Guid FileManifestId { get; init; }
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets content type.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets size bytes.
    /// </summary>
    public long SizeBytes { get; init; }
    /// <summary>
    /// Gets or sets the timestamp when the resource was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>
    /// Gets or sets version number.
    /// </summary>
    public int VersionNumber { get; init; }
    /// <summary>
    /// Indicates whether current.
    /// </summary>
    public bool IsCurrent { get; init; }
    /// <summary>
    /// Indicates whether original.
    /// </summary>
    public bool IsOriginal { get; init; }
    /// <summary>
    /// Indicates whether delete.
    /// </summary>
    public bool CanDelete { get; init; }
}
