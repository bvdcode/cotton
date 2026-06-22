// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents hls rendition profile.
    /// </summary>
    public static class HlsRenditionProfile
    {
        private static readonly HashSet<string> StreamCopyableAudioCodecs =
            new(StringComparer.OrdinalIgnoreCase) { "aac", "mp3" };

        /// <summary>
        /// Represents encoder plan.
        /// </summary>
        public record EncoderPlan(string VideoCodecArgs, string AudioCodecArgs, bool IsStreamCopy)
        {
            /// <summary>
            /// Gets the combined args.
            /// </summary>
            public string CombinedArgs => $"{VideoCodecArgs} {AudioCodecArgs}";
        }

        /// <summary>
        /// Parses value.
        /// </summary>
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

        /// <summary>
        /// Builds the plan decision for video playback.
        /// </summary>
        public static EncoderPlan Plan(HlsRendition rendition) =>
            Plan(rendition, videoCodec: null, audioCodec: null);

        /// <summary>
        /// Builds the plan decision for video playback.
        /// </summary>
        public static EncoderPlan Plan(HlsRendition rendition, string? videoCodec, string? audioCodec)
        {
            if (rendition == HlsRendition.Source && CanStreamCopy(videoCodec, audioCodec))
            {
                return new EncoderPlan(
                    VideoCodecArgs: "-c:v copy -bsf:v h264_mp4toannexb",
                    AudioCodecArgs: "-c:a copy",
                    IsStreamCopy: true);
            }

            var (preset, crf, videoFilter) = rendition switch
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
                    $"-c:a aac -b:a {audioBitrateKbps.ToString(CultureInfo.InvariantCulture)}k -ac 2",
                IsStreamCopy: false);
        }

        /// <summary>
        /// Indicates whether stream copy.
        /// </summary>
        public static bool CanStreamCopy(string? videoCodec, string? audioCodec) =>
            string.Equals(videoCodec, "h264", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(audioCodec)
            && StreamCopyableAudioCodecs.Contains(audioCodec);
    }
}
