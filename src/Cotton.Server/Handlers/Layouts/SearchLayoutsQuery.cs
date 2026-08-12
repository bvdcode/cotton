// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public class SearchLayoutsQuery(
        Guid userId,
        Guid layoutId,
        string query,
        int page,
        int pageSize) : IRequest<PagedResult<SearchResultDto>>
    {
        public Guid UserId { get; } = userId;

        public Guid LayoutId { get; } = layoutId;

        public string Query { get; } = query;

        public int Page { get; } = page;

        public int PageSize { get; } = pageSize;
    }
}
