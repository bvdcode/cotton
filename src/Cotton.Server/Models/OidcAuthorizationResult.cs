// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    public record OidcAuthorizationResult(string AuthorizationUrl, string State, DateTime ExpiresAt);
}
