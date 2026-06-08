// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents a successful custom GeoIP resolver test payload.
/// </summary>
public sealed class CustomGeoLookupTestResultDto
{
    /// <summary>
    /// Gets or sets tested input label.
    /// </summary>
    public string InputLabel { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets tested input value.
    /// </summary>
    public string InputValue { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets resolved country.
    /// </summary>
    public string? Country { get; init; }
    /// <summary>
    /// Gets or sets resolved region.
    /// </summary>
    public string? Region { get; init; }
    /// <summary>
    /// Gets or sets resolved city.
    /// </summary>
    public string? City { get; init; }
}
