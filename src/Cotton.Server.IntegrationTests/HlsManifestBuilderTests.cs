// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;
using System.Globalization;

namespace Cotton.Server.IntegrationTests;

public class HlsManifestBuilderTests
{
    [TestCase(0.0, 0, 0.0)]
    [TestCase(-5.0, 0, 0.0)]
    [TestCase(double.NaN, 0, 0.0)]
    [TestCase(double.PositiveInfinity, 0, 0.0)]
    [TestCase(3.0, 1, 3.0)]
    [TestCase(6.0, 1, 6.0)]
    [TestCase(12.0, 2, 6.0)]
    [TestCase(13.5, 3, 1.5)]
    [TestCase(3601.25, 601, 1.25)]
    public void Plan_UsesFixedSegmentBoundaries(double duration, int expectedSegments, double expectedLast)
    {
        var plan = HlsManifestBuilder.Plan(duration);

        Assert.Multiple(() =>
        {
            Assert.That(plan.SegmentCount, Is.EqualTo(expectedSegments));
            Assert.That(plan.LastSegmentSeconds, Is.EqualTo(expectedLast).Within(1e-3));
        });
    }

    [Test]
    public void Build_ProducesVodPlaylistWithSegmentsInOrder()
    {
        string manifest = HlsManifestBuilder.Build(
            durationSeconds: 13.5,
            segmentUrlFactory: i => $"/api/v1/files/abc/hls/seg-{i}.ts?token=T");

        var lines = manifest.Split('\n');
        var extinfDurations = new List<double>();
        var urls = new List<string>();
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
            Assert.That(manifest, Does.Contain("#EXT-X-TARGETDURATION:6"));
            Assert.That(manifest, Does.EndWith("#EXT-X-ENDLIST\n"));
            Assert.That(extinfDurations, Is.EqualTo(new[] { 6.0, 6.0, 1.5 }).Within(1e-3));
            Assert.That(urls, Is.EqualTo(new[]
            {
                "/api/v1/files/abc/hls/seg-0.ts?token=T",
                "/api/v1/files/abc/hls/seg-1.ts?token=T",
                "/api/v1/files/abc/hls/seg-2.ts?token=T",
            }));
        });
    }

    [Test]
    public void Build_ZeroDuration_EmitsValidEmptyVodPlaylist()
    {
        string manifest = HlsManifestBuilder.Build(0.0, _ => "unused");

        Assert.Multiple(() =>
        {
            Assert.That(manifest, Does.StartWith("#EXTM3U\n"));
            Assert.That(manifest, Does.Contain("#EXT-X-PLAYLIST-TYPE:VOD"));
            Assert.That(manifest, Does.EndWith("#EXT-X-ENDLIST\n"));
            Assert.That(manifest, Does.Not.Contain("#EXTINF"));
        });
    }

    [TestCase(0, 0.0)]
    [TestCase(1, 6.0)]
    [TestCase(47, 282.0)]
    [TestCase(-1, 0.0)]
    public void StartTimeOf_MapsIndexToSegmentBoundary(int segmentIndex, double expected)
    {
        Assert.That(HlsManifestBuilder.StartTimeOf(segmentIndex), Is.EqualTo(expected).Within(1e-6));
    }

    [Test]
    public void BuildMaster_EmitsStreamInfoEntriesInDeclaredOrder()
    {
        var variants = new[]
        {
            new HlsManifestBuilder.HlsVariant(
                Name: "Source",
                BandwidthBitsPerSecond: 8_000_000,
                Width: 1920,
                Height: 1080,
                Codecs: "avc1.640029,mp4a.40.2",
                PlaylistUrl: "playlist.m3u8?quality=source"),
            new HlsManifestBuilder.HlsVariant(
                Name: "720p",
                BandwidthBitsPerSecond: 1_500_000,
                Width: 1280,
                Height: 720,
                Codecs: "avc1.640029,mp4a.40.2",
                PlaylistUrl: "playlist.m3u8?quality=medium"),
        };

        string master = HlsManifestBuilder.BuildMaster(variants);
        var lines = master.Split('\n');
        var streamInfos = lines.Where(l => l.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal)).ToList();
        var urls = lines.Where(l => l.Contains("playlist.m3u8", StringComparison.Ordinal)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Is.EqualTo("#EXTM3U"));
            Assert.That(master, Does.Contain("#EXT-X-VERSION:4"));
            Assert.That(master, Does.Contain("#EXT-X-INDEPENDENT-SEGMENTS"));
            Assert.That(streamInfos, Has.Count.EqualTo(2));
            Assert.That(streamInfos[0], Does.Contain("BANDWIDTH=8000000"));
            Assert.That(streamInfos[0], Does.Contain("RESOLUTION=1920x1080"));
            Assert.That(streamInfos[0], Does.Contain("CODECS=\"avc1.640029,mp4a.40.2\""));
            Assert.That(streamInfos[0], Does.Contain("NAME=\"Source\""));
            Assert.That(streamInfos[1], Does.Contain("BANDWIDTH=1500000"));
            Assert.That(streamInfos[1], Does.Contain("RESOLUTION=1280x720"));
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
