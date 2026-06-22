// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Startup
{
    internal interface IStartupCheck
    {
        Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken);
    }
}
