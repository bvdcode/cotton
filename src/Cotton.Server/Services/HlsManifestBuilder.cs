// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Builds hls manifest values.
    /// </summary>
    public static class HlsManifestBuilder
    {
        /// <summary>
        /// Gets or sets the segment duration seconds.
        /// </summary>
        public const double SegmentDurationSeconds = 6.0;
        /// <summary>
        /// Defines the content type.
        /// </summary>
        public const string ContentType = "application/vnd.apple.mpegurl";

        /// <summary>
        /// Represents hls manifest plan.
        /// </summary>
        public sealed record HlsManifestPlan(int SegmentCount, double LastSegmentSeconds)
        {
            /// <summary>
            /// Gets the planned duration of a segment.
            /// </summary>
            public double DurationOf(int segmentIndex) =>
                segmentIndex == SegmentCount - 1 ? LastSegmentSeconds : SegmentDurationSeconds;
        }

        /// <summary>
        /// Represents hls variant.
        /// </summary>
        public sealed record HlsVariant(
            string Name,
            int BandwidthBitsPerSecond,
            int Width,
            int Height,
            string Codecs,
            string PlaylistUrl);

        /// <summary>
        /// Builds the plan decision for video playback.
        /// </summary>
        public static HlsManifestPlan Plan(double durationSeconds)
        {
            if (durationSeconds <= 0 || !double.IsFinite(durationSeconds))
            {
                return new HlsManifestPlan(0, 0);
            }

            int fullSegments = (int)Math.Floor(durationSeconds / SegmentDurationSeconds);
            double remainder = durationSeconds - (fullSegments * SegmentDurationSeconds);
            if (remainder > 1e-6)
            {
                return new HlsManifestPlan(fullSegments + 1, remainder);
            }

            return new HlsManifestPlan(Math.Max(1, fullSegments), SegmentDurationSeconds);
        }

        /// <summary>
        /// Builds value.
        /// </summary>
        public static string Build(double durationSeconds, Func<int, string> segmentUrlFactory)
        {
            ArgumentNullException.ThrowIfNull(segmentUrlFactory);

            var plan = Plan(durationSeconds);
            int targetDuration = (int)Math.Ceiling(SegmentDurationSeconds);
            var sb = new StringBuilder(256 + (64 * plan.SegmentCount));

            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:4\n");
            sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");
            sb.Append($"#EXT-X-TARGETDURATION:{targetDuration}\n");
            sb.Append("#EXT-X-MEDIA-SEQUENCE:0\n");
            sb.Append("#EXT-X-PLAYLIST-TYPE:VOD\n");

            for (int i = 0; i < plan.SegmentCount; i++)
            {
                sb.Append("#EXTINF:");
                sb.Append(plan.DurationOf(i).ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(",\n");
                sb.Append(segmentUrlFactory(i));
                sb.Append('\n');
            }

            sb.Append("#EXT-X-ENDLIST\n");
            return sb.ToString();
        }

        /// <summary>
        /// Builds master.
        /// </summary>
        public static string BuildMaster(IEnumerable<HlsVariant> variants)
        {
            ArgumentNullException.ThrowIfNull(variants);

            var sb = new StringBuilder(256);
            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:4\n");
            sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");

            foreach (var variant in variants)
            {
                sb.Append("#EXT-X-STREAM-INF:");
                sb.Append("BANDWIDTH=").Append(variant.BandwidthBitsPerSecond.ToString(CultureInfo.InvariantCulture));
                sb.Append(",RESOLUTION=").Append(variant.Width.ToString(CultureInfo.InvariantCulture));
                sb.Append('x').Append(variant.Height.ToString(CultureInfo.InvariantCulture));
                sb.Append(",CODECS=\"").Append(variant.Codecs).Append('"');
                sb.Append(",NAME=\"").Append(variant.Name).Append('"');
                sb.Append('\n');
                sb.Append(variant.PlaylistUrl).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the segment start time.
        /// </summary>
        public static double StartTimeOf(int segmentIndex) =>
            segmentIndex < 0 ? 0 : segmentIndex * SegmentDurationSeconds;
    }
}
