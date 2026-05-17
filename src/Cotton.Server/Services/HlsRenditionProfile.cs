// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Server.Services
{
    public enum HlsRendition
    {
        Source,
        High,
        Medium,
        Low,
    }

    public static class HlsRenditionProfile
    {
        public sealed record EncoderPlan(string VideoCodecArgs, string AudioCodecArgs)
        {
            public string CombinedArgs => $"{VideoCodecArgs} {AudioCodecArgs}";
        }

        public static HlsRendition Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return HlsRendition.Source;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "source" or "original" => HlsRendition.Source,
                "high" => HlsRendition.High,
                "medium" or "med" => HlsRendition.Medium,
                "low" => HlsRendition.Low,
                _ => HlsRendition.Source,
            };
        }

        public static EncoderPlan Plan(HlsRendition rendition)
        {
            (string preset, int crf, string videoFilter) = rendition switch
            {
                HlsRendition.Low => ("veryfast", 28, "scale=w=trunc(min(854\\,iw)/2)*2:h=-2"),
                HlsRendition.Medium => ("veryfast", 25, "scale=w=trunc(min(1280\\,iw)/2)*2:h=-2"),
                HlsRendition.High => ("veryfast", 23, "scale=w=trunc(min(1920\\,iw)/2)*2:h=-2"),
                _ => ("veryfast", 20, ""),
            };

            string filterArg = string.IsNullOrEmpty(videoFilter)
                ? string.Empty
                : $"-vf \"{videoFilter}\" ";

            int audioBitrateKbps = rendition switch
            {
                HlsRendition.Low => 96,
                HlsRendition.Medium => 128,
                _ => 192,
            };

            return new EncoderPlan(
                VideoCodecArgs:
                    $"{filterArg}-c:v libx264 -preset {preset} -crf {crf.ToString(CultureInfo.InvariantCulture)} "
                    + "-pix_fmt yuv420p -profile:v high -level 4.1",
                AudioCodecArgs:
                    $"-c:a aac -b:a {audioBitrateKbps.ToString(CultureInfo.InvariantCulture)}k -ac 2");
        }
    }
}
