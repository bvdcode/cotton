// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Selects how Cotton resolves IP addresses to approximate locations.
    /// </summary>
    public enum GeoIpLookupMode
    {
        /// <summary>
        /// Disable geolocation lookup.
        /// </summary>
        Disabled = 0,
        /// <summary>
        /// Use Cotton Bridge geolocation lookup.
        /// </summary>
        CottonCloud = 1,
        /// <summary>
        /// Use a local MaxMind database.
        /// </summary>
        MaxMindLocal = 2,
        /// <summary>
        /// Use a custom HTTP geolocation service.
        /// </summary>
        CustomHttp = 3
    }
}
