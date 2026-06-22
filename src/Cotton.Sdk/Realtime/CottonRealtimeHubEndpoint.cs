// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Realtime;

/// <summary>
/// Builds the Cotton realtime hub endpoint.
/// </summary>
public static class CottonRealtimeHubEndpoint
{
    /// <summary>
    /// Creates an absolute event hub URI from a server base address.
    /// </summary>
    public static Uri CreateUri(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        return CottonRouteUri.Create(baseAddress, Routes.V1.EventHub);
    }
}
