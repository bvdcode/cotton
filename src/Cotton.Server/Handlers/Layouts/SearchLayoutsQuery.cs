// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Represents a search layouts query sent through the mediator pipeline.
    /// </summary>
    public class SearchLayoutsQuery(
        Guid userId,
        Guid layoutId,
        string query,
        int page,
        int pageSize) : IRequest<PagedResult<SearchResultDto>>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;

        /// <summary>
        /// Gets the layout identifier.
        /// </summary>
        public Guid LayoutId { get; } = layoutId;

        /// <summary>
        /// Gets the query.
        /// </summary>
        public string Query { get; } = query;

        /// <summary>
        /// Gets the page.
        /// </summary>
        public int Page { get; } = page;

        /// <summary>
        /// Gets the page size.
        /// </summary>
        public int PageSize { get; } = pageSize;
    }
}
