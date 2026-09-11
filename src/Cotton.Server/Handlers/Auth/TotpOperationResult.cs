// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;

namespace Cotton.Server.Handlers.Auth
{
    public record TotpOperationResult(
        TotpOperationStatus Status,
        string? Error = null,
        TotpSetup? Setup = null);
}
