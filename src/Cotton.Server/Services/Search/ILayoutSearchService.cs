// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Defines the application layout search service.
    /// </summary>
    public interface ILayoutSearchService
    {
        /// <summary>
        /// Searches a layout and returns the API result payload.
        /// </summary>
        Task<SearchLayoutsResultDto> SearchAsync(LayoutSearchRequest request, CancellationToken cancellationToken);
    }
}
