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
        /// <summary>Removes backend-specific temporary files older than the supplied time-to-live.</summary>
        void CleanupTempFiles(TimeSpan ttl);
        /// <summary>Deletes a stored blob and returns whether it existed.</summary>
        Task<bool> DeleteAsync(string uid);
        /// <summary>Returns whether a blob with the supplied UID exists.</summary>
        Task<bool> ExistsAsync(string uid);
        /// <summary>Returns the stored blob size in bytes, or zero when the blob is missing.</summary>
        Task<long> GetSizeAsync(string uid);
        /// <summary>Opens a readable stream for a stored blob.</summary>
        Task<Stream> ReadAsync(string uid);
        /// <summary>Writes the supplied blob stream if the UID is not already present.</summary>
        Task WriteAsync(string uid, Stream stream);
        /// <summary>Lists every storage UID known to the backend for consistency checks.</summary>
        IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default);
    }
}
