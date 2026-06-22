// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Carries search scope and normalized criteria into a provider.
    /// </summary>
    public sealed record LayoutSearchProviderContext(
        LayoutSearchRequest Request,
        LayoutSearchCriteria Criteria);
}
