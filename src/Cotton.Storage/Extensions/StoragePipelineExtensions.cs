// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Streams;

namespace Cotton.Storage.Extensions
{
    /// <summary>
    /// Convenience helpers for storage pipeline stream composition.
    /// </summary>
    public static class StoragePipelineExtensions
    {
        /// <summary>Creates a stream that reads the supplied storage chunks as one contiguous blob.</summary>
        public static Stream GetBlobStream(this IStoragePipeline _storage, string[] uids, PipelineContext? pipelineContext = null)
        {
            return new ConcatenatedReadStream(storage: _storage, hashes: uids, pipelineContext);
        }
    }
}
