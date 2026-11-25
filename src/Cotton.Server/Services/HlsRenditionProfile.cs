// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public static class HlsRenditionProfile
    {
        public record EncoderPlan(int? MaximumWidth, int Quantizer, int AudioBitRate);

        public static HlsRendition Parse(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "source" or "original" => HlsRendition.Source,
                "high" => HlsRendition.High,
                "medium" or "med" => HlsRendition.Medium,
                "low" => HlsRendition.Low,
                _ => HlsRendition.Source,
            };

        public static EncoderPlan Plan(HlsRendition rendition) =>
            rendition switch
            {
                HlsRendition.Source => new(null, 30, 192_000),
                HlsRendition.High => new(1920, 32, 192_000),
                HlsRendition.Medium => new(1280, 31, 128_000),
                HlsRendition.Low => new(854, 34, 96_000),
                _ => throw new ArgumentOutOfRangeException(nameof(rendition)),
            };
    }
}
