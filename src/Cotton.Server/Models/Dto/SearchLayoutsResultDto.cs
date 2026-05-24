// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents the search layouts result API payload.
/// </summary>
public class SearchLayoutsResultDto : SearchResultDto
{
    /// <summary>
    /// Gets or sets total count.
    /// </summary>
    public int TotalCount { get; set; }
}
