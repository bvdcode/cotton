// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class MediaMetadataLimitExceededException(string limitName, int maxBytes)
        : Exception($"Media metadata exceeded the configured {limitName} limit of {maxBytes} bytes.")
    {
        public string LimitName { get; } = limitName;

        public int MaxBytes { get; } = maxBytes;
    }
}
