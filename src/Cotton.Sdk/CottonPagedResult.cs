// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk
{
    /// <summary>
    /// Pairs one page of an API payload with the total number of matching items.
    /// </summary>
    public record CottonPagedResult<T>(T Payload, int TotalCount);
}
