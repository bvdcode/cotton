// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Holds the keyring loaded after database migrations and before normal request handling starts.
/// </summary>
public sealed class KeyringRuntimeState
{
    private readonly object _gate = new();
    private KeyringBootstrapResult? _current;

    internal KeyringBootstrapResult? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    internal void Set(KeyringBootstrapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_gate)
        {
            _current = result;
        }
    }
}
