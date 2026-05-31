// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts;

/// <summary>
/// Represents a search layouts query sent through the mediator pipeline.
/// </summary>
public class SearchLayoutsQuery(
    Guid userId,
    Guid layoutId,
    string query,
    int page,
    int pageSize) : IRequest<SearchLayoutsResultDto>
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

/// <summary>
/// Handles search layouts queries in the mediator pipeline.
/// </summary>
public class SearchLayoutsQueryHandler(ILayoutSearchService _searchService)
    : IRequestHandler<SearchLayoutsQuery, SearchLayoutsResultDto>
{
    /// <summary>
    /// Handles the request through the mediator pipeline.
    /// </summary>
    public Task<SearchLayoutsResultDto> Handle(SearchLayoutsQuery request, CancellationToken cancellationToken)
    {
        LayoutSearchRequest searchRequest = new(
            request.UserId,
            request.LayoutId,
            request.Query,
            request.Page,
            request.PageSize);

        return _searchService.SearchAsync(searchRequest, cancellationToken);
    }
}
