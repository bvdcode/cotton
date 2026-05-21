// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Pipelines
{
    public class PipelineContext
    {
        public long? FileSizeBytes { get; set; }
        public bool StoreInMemoryCache { get; set; }
        public Dictionary<string, long>? ChunkLengths { get; set; }
    }
}
