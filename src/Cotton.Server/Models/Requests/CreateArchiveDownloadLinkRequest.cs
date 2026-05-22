// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests;

/// <summary>
/// Represents the create archive download link request request payload accepted by the API.
/// </summary>
public sealed class CreateArchiveDownloadLinkRequest
{
    /// <summary>
    /// Gets or sets file ids.
    /// </summary>
    public IReadOnlyList<Guid> FileIds { get; init; } = [];
    /// <summary>
    /// Gets or sets node ids.
    /// </summary>
    public IReadOnlyList<Guid> NodeIds { get; init; } = [];
    /// <summary>
    /// Gets or sets archive name.
    /// </summary>
    public string? ArchiveName { get; init; }
}
