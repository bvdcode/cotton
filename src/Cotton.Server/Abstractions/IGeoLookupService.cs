// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using System.Net;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the geo lookup service contract used by the server runtime.
    /// </summary>
    public interface IGeoLookupService
    {
        /// <summary>
        /// Attempts to lookup.
        /// </summary>
        Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
        /// <summary>
        /// Tests a custom lookup resolver.
        /// </summary>
        Task<CustomGeoLookupTestResult> TestCustomLookupAsync(
            string serverBaseUrl,
            CancellationToken cancellationToken = default);
    }
}
