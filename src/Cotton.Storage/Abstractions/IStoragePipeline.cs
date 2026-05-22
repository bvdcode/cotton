// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Composes storage processors with the selected backend to read and write Cotton blobs.
    /// </summary>
    public interface IStoragePipeline
    {
        /// <summary>Deletes a stored blob and returns whether it existed.</summary>
        Task<bool> DeleteAsync(string uid);
        /// <summary>Returns whether a blob with the supplied UID exists.</summary>
        Task<bool> ExistsAsync(string uid);
        /// <summary>Returns the stored blob size in bytes, or zero when the blob is missing.</summary>
        Task<long> GetSizeAsync(string uid);
        /// <summary>Opens a readable stream and applies read processors in pipeline order.</summary>
        Task<Stream> ReadAsync(string uid, PipelineContext? context = null);
        /// <summary>Applies write processors and stores the resulting stream in the backend.</summary>
        Task WriteAsync(string uid, Stream stream, PipelineContext? context = null);
        /// <summary>Lists every storage UID known to the active backend.</summary>
        IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default);
    }
}
