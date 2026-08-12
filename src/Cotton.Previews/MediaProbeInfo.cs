// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xabe.FFmpeg.Downloader;

namespace Cotton.Previews
{
    public record MediaProbeInfo(double? DurationSeconds, string? VideoCodec, string? AudioCodec);
}
