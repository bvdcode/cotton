// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Streams;

namespace Cotton.Storage.Extensions
{
    public static class StoragePipelineExtensions
    {
        public static Stream GetBlobStream(this IStoragePipeline _storage, string[] uids, PipelineContext? pipelineContext = null)
        {
            ArgumentNullException.ThrowIfNull(uids);
            foreach (var uid in uids)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            }
            return new ConcatenatedReadStream(storage: _storage, hashes: uids, pipelineContext);
        }
    }
}
