// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Contracts.Auth;

/// <summary>
/// Represents a request that carries a refresh token in the HTTP body.
/// </summary>
public sealed class RefreshTokenRequestDto
{
    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
