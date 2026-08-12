// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public record LayoutSearchRequest(
        Guid UserId,
        Guid LayoutId,
        string Query,
        int Page,
        int PageSize);
}
