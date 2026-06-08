// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    /// <summary>
    /// Represents the result of geo lookup.
    /// </summary>
    public record GeoLookupResult(string? Country, string? Region, string? City);

    /// <summary>
    /// Represents the result of testing a custom geo lookup resolver.
    /// </summary>
    public record CustomGeoLookupTestResult(
        string? Error,
        string? InputLabel,
        string? InputValue,
        GeoLookupResult? Result);
}
