// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{

    /// <summary>
    /// Computes content hashes for local file snapshots when reconciliation needs them.
    /// </summary>
    public interface ILocalFileContentHasher
    {
        /// <summary>
        /// Computes the lowercase SHA-256 content hash for a local file snapshot.
        /// </summary>
        Task<string> ComputeContentHashAsync(LocalFileSnapshot localFile, CancellationToken cancellationToken = default);
    }
}
