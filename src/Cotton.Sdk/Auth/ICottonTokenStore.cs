// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Auth;

namespace Cotton.Sdk.Auth;

/// <summary>
/// Persists Cotton access and refresh tokens for SDK requests.
/// </summary>
public interface ICottonTokenStore
{
    /// <summary>
    /// Loads the current token pair, or null when the client is not authenticated.
    /// </summary>
    Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a newly issued token pair.
    /// </summary>
    Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears any persisted token pair.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
