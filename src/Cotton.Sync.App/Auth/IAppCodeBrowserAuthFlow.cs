// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Coordinates browser app-code authentication for native clients.
    /// </summary>
    public interface IAppCodeBrowserAuthFlow
    {
        /// <summary>
        /// Starts browser approval, waits for completion, and returns the authenticated session.
        /// </summary>
        Task<AuthSession> SignInAsync(
            AppCodeBrowserSignInRequest request,
            CancellationToken cancellationToken = default);
    }
}
