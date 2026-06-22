// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    /// <summary>
    /// Media metadata extracted by ffprobe for preview and playback planning.
    /// </summary>
    public record MediaProbeInfo(double? DurationSeconds, string? VideoCodec, string? AudioCodec);
}
