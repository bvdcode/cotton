// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class GcChunkTimelineBucketDto
    {
        public DateTime BucketStartUtc { get; init; }
        public long ChunkCount { get; init; }
        public long SizeBytes { get; init; }
    }
}
