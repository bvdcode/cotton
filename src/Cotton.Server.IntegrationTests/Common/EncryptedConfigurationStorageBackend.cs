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

        public Task<bool> DeleteAsync(string uid, CancellationToken cancellationToken = default) => _inner.DeleteAsync(uid);

        public Task<bool> ExistsAsync(string uid, CancellationToken cancellationToken = default) => _inner.ExistsAsync(uid);

        public Task<long> GetSizeAsync(string uid, CancellationToken cancellationToken = default) => _inner.GetSizeAsync(uid);

        public Task<Stream> ReadAsync(string uid, CancellationToken cancellationToken = default) => _inner.ReadAsync(uid);

        public Task<long> WriteAsync(string uid, Stream stream, bool overwrite = false, CancellationToken cancellationToken = default) => _inner.WriteAsync(uid, stream, overwrite);

        public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default) =>
            _inner.ListAllKeysAsync(ct);

        public IAsyncEnumerable<string> ListKeysByPrefixAsync(char prefix, CancellationToken ct = default) =>
            _inner.ListKeysByPrefixAsync(prefix, ct);
    }
}
