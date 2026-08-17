// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Low-level object store used by the storage pipeline after processors have transformed the stream.
    /// </summary>
    /// <remarks>
    /// Backends store opaque chunk blobs addressed by normalized content UIDs. They must not know about users, files,
    /// manifests, encryption keys, or quotas; those concerns live above this boundary.
    /// </remarks>
    public interface IStorageBackend
    {
        void CleanupTempFiles(TimeSpan ttl);

        Task<bool> DeleteAsync(string uid);

        Task<bool> ExistsAsync(string uid);

        Task<long> GetSizeAsync(string uid);

        Task<Stream> ReadAsync(string uid);

        Task<long> WriteAsync(string uid, Stream stream);

        IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default);
    }
}
