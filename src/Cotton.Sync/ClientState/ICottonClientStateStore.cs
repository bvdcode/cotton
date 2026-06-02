// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Auth;

namespace Cotton.Sync.ClientState;

/// <summary>
/// Persists authentication tokens and client profile values for Cotton sync clients.
/// </summary>
public interface ICottonClientStateStore : ICottonTokenStore
{
    /// <summary>
    /// Saves the Cotton server base address associated with the current profile.
    /// </summary>
    Task SaveServerBaseAddressAsync(Uri serverBaseAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the Cotton server base address associated with the current profile.
    /// </summary>
    Task<Uri?> GetServerBaseAddressAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a client profile value.
    /// </summary>
    Task SaveProfileValueAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a client profile value.
    /// </summary>
    Task<string?> GetProfileValueAsync(string key, CancellationToken cancellationToken = default);
}
