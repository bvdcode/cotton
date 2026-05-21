// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    public enum GeoIpLookupMode
    {
        Disabled = 0,
        CottonCloud = 1,
        MaxMindLocal = 2,
        CustomHttp = 3
    }
}
