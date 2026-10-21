// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;

namespace Cotton.Sdk.Auth
{
    /// <summary>
    /// Persists Cotton access and refresh tokens for SDK requests.
    /// </summary>
    /// <remarks>
    /// Share one instance between SDK clients using the same server and account. The SDK coordinates
    /// refresh, sign-in persistence and logout per store instance within the process. Implementations
    /// must support concurrent reads and persist a complete pair before SaveAsync completes.
    /// </remarks>
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
}
