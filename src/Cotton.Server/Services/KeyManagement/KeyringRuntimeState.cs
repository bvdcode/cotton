// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Holds the keyring loaded after database migrations and before normal request handling starts.
/// </summary>
internal sealed class KeyringRuntimeState
{
    private readonly object _gate = new();
    private KeyringBootstrapResult? _current;

    public KeyringBootstrapResult? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Set(KeyringBootstrapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_gate)
        {
            _current = result;
        }
    }
}
