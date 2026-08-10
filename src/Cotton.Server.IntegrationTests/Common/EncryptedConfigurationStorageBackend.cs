// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Server.IntegrationTests.Common
{
    internal class EncryptedConfigurationStorageBackend(IStorageBackend _inner) :
        IStorageBackend,
        IStorageBackendUsesEncryptedConfiguration
    {
        public void CleanupTempFiles(TimeSpan ttl) => _inner.CleanupTempFiles(ttl);

        public Task<bool> DeleteAsync(string uid) => _inner.DeleteAsync(uid);

        public Task<bool> ExistsAsync(string uid) => _inner.ExistsAsync(uid);

        public Task<long> GetSizeAsync(string uid) => _inner.GetSizeAsync(uid);

        public Task<Stream> ReadAsync(string uid) => _inner.ReadAsync(uid);

        public Task WriteAsync(string uid, Stream stream) => _inner.WriteAsync(uid, stream);

        public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default) =>
            _inner.ListAllKeysAsync(ct);
    }
}
