// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Extensions
{
    internal record NotificationClientContext(
        string Ip,
        string UserAgent,
        string DeviceName,
        bool HasDevice,
        string Location,
        string Country,
        string Region,
        string City);
}
