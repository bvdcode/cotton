// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public enum AppCodePollStatus
    {
        NotFound,
        Expired,
        Denied,
        Approved,
        Pending,
    }
}
