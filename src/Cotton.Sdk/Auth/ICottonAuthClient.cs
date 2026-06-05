// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Auth;

namespace Cotton.Sdk.Auth;

/// <summary>
/// Provides authentication operations for Cotton Cloud.
/// </summary>
public interface ICottonAuthClient
{
    /// <summary>
    /// Authenticates and stores the issued token pair.
    /// </summary>
    Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the current access token.
    /// </summary>
    Task<TokenPairDto> RefreshAsync(string? refreshToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the current refresh token and clears local tokens.
    /// </summary>
    Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    Task<UserDto> MeAsync(CancellationToken cancellationToken = default);
}
