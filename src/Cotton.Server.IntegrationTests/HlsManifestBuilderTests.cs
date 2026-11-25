// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Mediana.Core.Timing;
using NUnit.Framework;
using System.Globalization;

namespace Cotton.Server.IntegrationTests
{
    public class HlsManifestBuilderTests
    {
        [Test]
        public void Build_ProducesVodPlaylistWithSegmentsInOrder()
        {
            TimeBase clock = new(1, 1000);
            MediaTimeRange[] segments =
            [
                new(new(0, clock), new(6006, clock)),
                new(new(6006, clock), new(12012, clock)),
                new(new(12012, clock), new(13500, clock)),
            ];
            string manifest = HlsManifestBuilder.Build(segments, i => $"/api/v1/files/abc/hls/seg-{i}.ts?token=T");

            string[] lines = manifest.Split('\n');
            List<double> extinfDurations = new List<double>();
            List<string> urls = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("#EXTINF:", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = lines[i]["#EXTINF:".Length..].TrimEnd(',');
                extinfDurations.Add(double.Parse(value, CultureInfo.InvariantCulture));
                urls.Add(lines[i + 1]);
            }

            Assert.Multiple(() =>
            {
                Assert.That(manifest, Does.StartWith("#EXTM3U\n"));
                Assert.That(manifest, Does.Contain("#EXT-X-VERSION:4"));
                Assert.That(manifest, Does.Contain("#EXT-X-INDEPENDENT-SEGMENTS"));
                Assert.That(manifest, Does.Contain("#EXT-X-PLAYLIST-TYPE:VOD"));
                Assert.That(manifest, Does.Contain("#EXT-X-TARGETDURATION:7"));
                Assert.That(manifest, Does.EndWith("#EXT-X-ENDLIST\n"));
                Assert.That(extinfDurations, Is.EqualTo(new[] { 6.006, 6.006, 1.488 }).Within(1e-3));
                Assert.That(urls, Is.EqualTo(new[]
                {
                    "/api/v1/files/abc/hls/seg-0.ts?token=T",
                    "/api/v1/files/abc/hls/seg-1.ts?token=T",
                    "/api/v1/files/abc/hls/seg-2.ts?token=T",
                }));
            });
        }

        [Test]
        public void BuildMaster_EmitsStreamInfoEntriesInDeclaredOrder()
        {
            HlsManifestBuilder.HlsVariant[] variants = new[]
            {
                new HlsManifestBuilder.HlsVariant(
                    Name: "Source",
                    BandwidthBitsPerSecond: 8_000_000,
                    PlaylistUrl: "playlist.m3u8?quality=source"),
                new HlsManifestBuilder.HlsVariant(
                    Name: "720p",
                    BandwidthBitsPerSecond: 1_500_000,
                    PlaylistUrl: "playlist.m3u8?quality=medium"),
            };

            string master = HlsManifestBuilder.BuildMaster(variants);
            string[] lines = master.Split('\n');
            List<string> streamInfos = lines.Where(l => l.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal)).ToList();
            List<string> urls = lines.Where(l => l.Contains("playlist.m3u8", StringComparison.Ordinal)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(lines[0], Is.EqualTo("#EXTM3U"));
                Assert.That(master, Does.Contain("#EXT-X-VERSION:4"));
                Assert.That(master, Does.Contain("#EXT-X-INDEPENDENT-SEGMENTS"));
                Assert.That(streamInfos, Has.Count.EqualTo(2));
                Assert.That(streamInfos[0], Does.Contain("BANDWIDTH=8000000"));
                Assert.That(streamInfos[0], Does.Contain("NAME=\"Source\""));
                Assert.That(streamInfos[1], Does.Contain("BANDWIDTH=1500000"));
                Assert.That(urls, Is.EqualTo(new[]
                {
                    "playlist.m3u8?quality=source",
                    "playlist.m3u8?quality=medium",
                }));
                Assert.That(master, Does.Not.Contain("#EXT-X-ENDLIST"));
                Assert.That(master, Does.Not.Contain("#EXTINF"));
            });
        }
    }
}
