// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class ArchiveDownloadLinkDto
    {
        public string Url { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;
    }
}
