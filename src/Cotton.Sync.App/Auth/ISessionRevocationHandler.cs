// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth;

/// <summary>
/// Handles server-driven authentication session revocation.
/// </summary>
public interface ISessionRevocationHandler
{
    /// <summary>
    /// Stops authenticated background work and clears the local session.
    /// </summary>
    Task HandleSessionRevokedAsync(CancellationToken cancellationToken = default);
}
