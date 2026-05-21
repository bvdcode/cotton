// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using System.Net;

namespace Cotton.Server.Abstractions
{
    public interface IGeoLookupService
    {
        Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
        Task<string?> TestCustomLookupAsync(string serverBaseUrl, CancellationToken cancellationToken = default);
    }
}
