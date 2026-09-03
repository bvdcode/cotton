// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal record OidcIdentityClaims(
        string Issuer,
        string Subject,
        string? Email,
        bool EmailVerified,
        string? Name,
        string? GivenName,
        string? FamilyName,
        string? PictureUrl,
        string? PreferredUsername);
}
