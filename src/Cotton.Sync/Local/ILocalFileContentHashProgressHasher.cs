// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Computes local content hashes and reports byte-level hashing progress.
    /// </summary>
    public interface ILocalFileContentHashProgressHasher : ILocalFileContentHasher
    {
        /// <summary>
        /// Computes the lowercase SHA-256 content hash for a local file snapshot while reporting progress.
        /// </summary>
        Task<string> ComputeContentHashAsync(
            LocalFileSnapshot localFile,
            IProgress<SyncTransferProgress>? progress,
            CancellationToken cancellationToken = default);
    }
}
