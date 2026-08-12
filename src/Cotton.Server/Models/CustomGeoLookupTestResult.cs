// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    public record CustomGeoLookupTestResult(
        string? Error,
        string? InputLabel,
        string? InputValue,
        GeoLookupResult? Result);
}
