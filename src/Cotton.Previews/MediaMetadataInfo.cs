// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Media metadata extracted by ffprobe.
    /// </summary>
    public record MediaMetadataInfo(
        double? DurationSeconds,
        string? VideoCodec,
        string? AudioCodec,
        int? Width,
        int? Height,
        IReadOnlyDictionary<string, string> Tags);
}
