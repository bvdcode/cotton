// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Extensions
{
    public static class StoragePipelineExtensions
    {
        public static Stream GetBlobStream(this IStoragePipeline _storage, string[] uids)
        {
            ArgumentNullException.ThrowIfNull(uids);
            foreach (var uid in uids)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            }
            return new Streams.ConcatenatedReadStream(storage: _storage, hashes: uids);
        }
    }
}
