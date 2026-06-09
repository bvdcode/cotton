// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Coordinates desktop authentication flows.
    /// </summary>
    public interface IAuthFlow
    {
        /// <summary>
        /// Signs in with username/password credentials and optional TOTP.
        /// </summary>
        Task<AuthSession> SignInAsync(PasswordSignInRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores the current session from the underlying SDK token store.
        /// </summary>
        Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out and clears the underlying SDK token store.
        /// </summary>
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }
}
