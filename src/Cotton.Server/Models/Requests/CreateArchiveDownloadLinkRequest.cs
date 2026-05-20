// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests;

public sealed class CreateArchiveDownloadLinkRequest
{
    public IReadOnlyList<Guid> FileIds { get; init; } = [];
    public IReadOnlyList<Guid> NodeIds { get; init; } = [];
    public string? ArchiveName { get; init; }
}
