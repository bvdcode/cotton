// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using EasyExtensions.Abstractions;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Produces purpose-specific ciphers for encrypted database fields and tokens.
/// </summary>
public interface IKeyringPurposeCipherFactory
{
    /// <summary>Creates a cipher for general encrypted database fields.</summary>
    IStreamCipher CreateDbFieldCipher();

    /// <summary>Creates a cipher for TOTP shared secrets.</summary>
    IStreamCipher CreateTotpSecretCipher();
}

/// <summary>
/// Produces purpose-specific stream ciphers while preserving the legacy cipher when keyring v2 is disabled.
/// </summary>
internal sealed class KeyringPurposeCipherFactory : IKeyringPurposeCipherFactory, IDisposable
{
    private readonly CottonEncryptionSettings _settings;
    private readonly KeyringPlainState? _keyringState;
    private readonly Dictionary<KeyringKeyPurpose, IStreamCipher> _ciphers = [];
    private bool _disposed;

    public KeyringPurposeCipherFactory(CottonEncryptionSettings settings, KeyringBootstrapResult? keyring)
        : this(settings, keyring?.State)
    {
    }

    internal KeyringPurposeCipherFactory(CottonEncryptionSettings settings, KeyringPlainState? keyringState)
    {
        _settings = settings;
        _keyringState = keyringState;
    }

    public IStreamCipher CreateDbFieldCipher()
    {
        return Create(KeyringKeyPurpose.DbFieldAead);
    }

    public IStreamCipher CreateTotpSecretCipher()
    {
        return Create(KeyringKeyPurpose.TotpSecretAead);
    }

    internal IStreamCipher Create(KeyringKeyPurpose purpose)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ciphers.TryGetValue(purpose, out IStreamCipher? cipher))
        {
            return cipher;
        }

        IStreamCipher created = _keyringState is null
            ? StreamCipherFactory.Create(_settings, SettingsProvider.GetCachedEncryptionThreads())
            : StreamCipherFactory.Create(
                _keyringState,
                _settings,
                purpose,
                SettingsProvider.GetCachedEncryptionThreads());
        _ciphers[purpose] = created;
        return created;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (IStreamCipher cipher in _ciphers.Values)
        {
            if (cipher is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _ciphers.Clear();
        _disposed = true;
    }
}
