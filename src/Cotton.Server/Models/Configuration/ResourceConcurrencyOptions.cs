// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    public class ResourceConcurrencyOptions
    {
        public const string SectionName = "ResourceConcurrency";

        public int HlsTranscodes { get; set; } = 2;

        public int HlsProbes { get; set; } = 2;

        public int ArchiveStreams { get; set; } = 4;

        public int StorageWrites { get; set; } = 8;

        public void Validate()
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HlsTranscodes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HlsProbes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ArchiveStreams);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(StorageWrites);
        }
    }
}
