// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Pipelines
{
    /// <summary>
    /// Optional per-operation metadata shared by storage processors.
    /// </summary>
    public class PipelineContext
    {
        /// <summary>Gets or sets the plaintext file size when the caller already knows it.</summary>
        public long? FileSizeBytes { get; set; }
        /// <summary>Gets or sets whether processors may keep small transformed blobs in memory.</summary>
        public bool StoreInMemoryCache { get; set; }
        /// <summary>Gets or sets known chunk lengths keyed by storage UID.</summary>
        public Dictionary<string, long>? ChunkLengths { get; set; }
    }
}
