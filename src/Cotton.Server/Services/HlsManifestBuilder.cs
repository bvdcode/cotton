// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Core.Timing;
using System.Globalization;
using System.Text;

namespace Cotton.Server.Services
{
    public static class HlsManifestBuilder
    {
        public const int SegmentDurationSeconds = 6;

        public const string ContentType = "application/vnd.apple.mpegurl";

        public record HlsVariant(string Name, int BandwidthBitsPerSecond, string PlaylistUrl);

        public static string Build(IReadOnlyList<MediaTimeRange> segments, Func<int, string> segmentUrlFactory)
        {
            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(segmentUrlFactory);

            int targetDuration = Math.Max(1, (int)Math.Ceiling(segments.Max(Seconds)));
            StringBuilder sb = new(256 + (64 * segments.Count));

            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:4\n");
            sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");
            sb.Append($"#EXT-X-TARGETDURATION:{targetDuration}\n");
            sb.Append("#EXT-X-MEDIA-SEQUENCE:0\n");
            sb.Append("#EXT-X-PLAYLIST-TYPE:VOD\n");

            for (int i = 0; i < segments.Count; i++)
            {
                sb.Append("#EXTINF:");
                sb.Append(Seconds(segments[i]).ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(",\n");
                sb.Append(segmentUrlFactory(i));
                sb.Append('\n');
            }

            sb.Append("#EXT-X-ENDLIST\n");
            return sb.ToString();
        }

        public static string BuildMaster(IEnumerable<HlsVariant> variants)
        {
            ArgumentNullException.ThrowIfNull(variants);

            StringBuilder sb = new StringBuilder(256);
            sb.Append("#EXTM3U\n");
            sb.Append("#EXT-X-VERSION:4\n");
            sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");

            foreach (HlsVariant variant in variants)
            {
                sb.Append("#EXT-X-STREAM-INF:");
                sb.Append("BANDWIDTH=").Append(variant.BandwidthBitsPerSecond.ToString(CultureInfo.InvariantCulture));
                sb.Append(",NAME=\"").Append(variant.Name).Append('"');
                sb.Append('\n');
                sb.Append(variant.PlaylistUrl).Append('\n');
            }

            return sb.ToString();
        }

        private static double Seconds(MediaTimeRange segment)
        {
            MediaDuration duration = segment.Duration;
            return duration.Value * (double)duration.TimeBase.Numerator / duration.TimeBase.Denominator;
        }
    }
}
