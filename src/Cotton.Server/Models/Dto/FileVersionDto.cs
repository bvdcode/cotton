// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

public sealed class FileVersionDto
{
    public Guid Id { get; init; }
    public Guid NodeFileId { get; init; }
    public Guid FileManifestId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
    public int VersionNumber { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsOriginal { get; init; }
    public bool CanDelete { get; init; }
}
