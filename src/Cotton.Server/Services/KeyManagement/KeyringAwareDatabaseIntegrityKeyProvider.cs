// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.DatabaseIntegrity;
using System.Security.Cryptography;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Chooses keyring-backed DB integrity keys once a runtime keyring is loaded, otherwise keeps legacy behavior.
/// </summary>
internal sealed class KeyringAwareDatabaseIntegrityKeyProvider :
    IDatabaseIntegrityKeyProvider,
    IDatabaseIntegrityVerificationKeyProvider,
    IDisposable
{
    private readonly DatabaseIntegrityKeyProvider _legacyProvider;
    private readonly KeyringRuntimeState? _runtimeState;
    private readonly KeyringBootstrapResult? _staticKeyring;

    public KeyringAwareDatabaseIntegrityKeyProvider(
        CottonEncryptionSettings settings,
        KeyringRuntimeState? runtimeState = null,
        KeyringBootstrapResult? staticKeyring = null)
    {
        _legacyProvider = new DatabaseIntegrityKeyProvider(settings);
        _runtimeState = runtimeState;
        _staticKeyring = staticKeyring;
    }

    public HMACSHA256 CreateHmac()
    {
        KeyringBootstrapResult? keyring = ResolveKeyring();
        return keyring is null
            ? _legacyProvider.CreateHmac()
            : new KeyringDatabaseIntegrityKeyProvider(keyring.State).CreateHmac();
    }

    public IReadOnlyList<HMACSHA256> CreateVerificationHmacs()
    {
        KeyringBootstrapResult? keyring = ResolveKeyring();
        return keyring is null
            ? _legacyProvider.CreateVerificationHmacs()
            : new KeyringDatabaseIntegrityKeyProvider(keyring.State).CreateVerificationHmacs();
    }

    public void Dispose()
    {
        _legacyProvider.Dispose();
    }

    private KeyringBootstrapResult? ResolveKeyring()
    {
        return _runtimeState?.Current ?? _staticKeyring;
    }
}
