// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Providers
{
    public class ServerSettingsCache
    {
        private static readonly TimeSpan BooleanCacheLifetime = TimeSpan.FromMinutes(1);
        private readonly Lock _gate = new();
        private readonly SemaphoreSlim _settingsCreationGate = new(1, 1);
        private ServerSettingsSnapshot? _settings;
        private int _encryptionThreads;
        private (bool Value, DateTimeOffset CachedAt)? _isServerInitialized;
        private (bool Value, DateTimeOffset CachedAt)? _serverHasUsers;

        internal ServerSettingsSnapshot GetOrAdd(Func<ServerSettingsSnapshot> factory)
        {
            ServerSettingsSnapshot? settings = Volatile.Read(ref _settings);
            if (settings is not null)
            {
                return settings;
            }

            lock (_gate)
            {
                settings = _settings;
                if (settings is not null)
                {
                    return settings;
                }

                settings = factory();
                _settings = settings;
                CacheEncryptionThreads(settings.EncryptionThreads);
                return settings;
            }
        }

        internal async Task<T> RunCreationExclusiveAsync<T>(
            Func<Task<T>> action,
            CancellationToken cancellationToken)
        {
            await _settingsCreationGate.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                _settingsCreationGate.Release();
            }
        }

        internal int? GetEncryptionThreads()
        {
            int value = Volatile.Read(ref _encryptionThreads);
            return value > 0 ? value : null;
        }

        internal void CacheEncryptionThreads(int encryptionThreads)
        {
            int value = encryptionThreads > 0 ? encryptionThreads : 0;
            Volatile.Write(ref _encryptionThreads, value);
        }

        internal bool TryGetServerInitialized(out bool value)
        {
            lock (_gate)
            {
                return TryGetBoolean(_isServerInitialized, out value);
            }
        }

        internal void SetServerInitialized(bool value)
        {
            lock (_gate)
            {
                _isServerInitialized = (value, DateTimeOffset.UtcNow);
            }
        }

        internal bool TryGetServerHasUsers(out bool value)
        {
            lock (_gate)
            {
                return TryGetBoolean(_serverHasUsers, out value);
            }
        }

        internal void SetServerHasUsers(bool value)
        {
            lock (_gate)
            {
                _serverHasUsers = (value, DateTimeOffset.UtcNow);
            }
        }

        internal void InvalidateSettings(bool serverIsInitialized)
        {
            lock (_gate)
            {
                _settings = null;
                if (serverIsInitialized)
                {
                    _isServerInitialized = (true, DateTimeOffset.UtcNow);
                }
            }
        }

        private static bool TryGetBoolean(
            (bool Value, DateTimeOffset CachedAt)? cached,
            out bool value)
        {
            if (cached is { } entry && DateTimeOffset.UtcNow - entry.CachedAt < BooleanCacheLifetime)
            {
                value = entry.Value;
                return true;
            }

            value = false;
            return false;
        }
    }
}
