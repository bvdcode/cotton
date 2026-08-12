// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    /// <summary>
    /// Pairs an API payload with the total item count reported through the X-Total-Count response header.
    /// </summary>
    public record PagedResult<T>(T Payload, int TotalCount);
}
