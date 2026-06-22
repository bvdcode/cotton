// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class HlsRenditionProfileTests
{
    [TestCase("source", HlsRendition.Source)]
    [TestCase("Source", HlsRendition.Source)]
    [TestCase("ORIGINAL", HlsRendition.Source)]
    [TestCase("high", HlsRendition.High)]
    [TestCase("medium", HlsRendition.Medium)]
    [TestCase("med", HlsRendition.Medium)]
    [TestCase("low", HlsRendition.Low)]
    [TestCase(null, HlsRendition.Source)]
    [TestCase("", HlsRendition.Source)]
    [TestCase("garbage", HlsRendition.Source)]
    public void Parse_NormalizesKnownAliases(string? input, HlsRendition expected)
    {
        Assert.That(HlsRenditionProfile.Parse(input), Is.EqualTo(expected));
    }

    [TestCase(HlsRendition.Source)]
    [TestCase(HlsRendition.High)]
    [TestCase(HlsRendition.Medium)]
    [TestCase(HlsRendition.Low)]
    public void Plan_WithoutProbeInfo_UsesBrowserCompatibleCodecs(HlsRendition rendition)
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(rendition);

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsStreamCopy, Is.False);
            Assert.That(plan.VideoCodecArgs, Does.Contain("libx264"));
            Assert.That(plan.VideoCodecArgs, Does.Not.Contain("-c:v copy"));
            Assert.That(plan.AudioCodecArgs, Does.Contain("-c:a aac"));
            Assert.That(plan.AudioCodecArgs, Does.Not.Contain("-c:a copy"));
        });
    }

    [TestCase("h264", "aac")]
    [TestCase("H264", "AAC")]
    [TestCase("h264", "mp3")]
    public void Plan_Source_StreamCopiesH264WhenAudioIsCompatible(
        string videoCodec,
        string? audioCodec)
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(HlsRendition.Source, videoCodec, audioCodec);

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsStreamCopy, Is.True);
            Assert.That(plan.VideoCodecArgs, Does.Contain("-c:v copy"));
            Assert.That(plan.VideoCodecArgs, Does.Contain("h264_mp4toannexb"));
            Assert.That(plan.AudioCodecArgs, Is.EqualTo("-c:a copy"));
        });
    }

    [TestCase("hevc", "aac")]
    [TestCase("h264", "opus")]
    [TestCase("h264", null)]
    [TestCase("vp9", "aac")]
    [TestCase(null, "aac")]
    public void Plan_Source_ReencodesWhenStreamCopyWouldBeRisky(
        string? videoCodec,
        string? audioCodec)
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(HlsRendition.Source, videoCodec, audioCodec);

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsStreamCopy, Is.False);
            Assert.That(plan.VideoCodecArgs, Does.Contain("libx264"));
            Assert.That(plan.AudioCodecArgs, Does.Contain("-c:a aac"));
        });
    }

    [Test]
    public void Plan_NonSourceRenditionsAlwaysReencode()
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(HlsRendition.Medium, "h264", "aac");

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsStreamCopy, Is.False);
            Assert.That(plan.VideoCodecArgs, Does.Contain("libx264"));
            Assert.That(plan.AudioCodecArgs, Does.Contain("-c:a aac"));
        });
    }

    [Test]
    public void Plan_Source_KeepsOriginalResolution()
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(HlsRendition.Source);

        Assert.Multiple(() =>
        {
            Assert.That(plan.VideoCodecArgs, Does.Contain("crf 20"));
            Assert.That(plan.VideoCodecArgs, Does.Not.Contain("-vf"));
        });
    }

    [TestCase(HlsRendition.Low, "854", "96k")]
    [TestCase(HlsRendition.Medium, "1280", "128k")]
    [TestCase(HlsRendition.High, "1920", "192k")]
    public void Plan_QualityTiersApplyExpectedScaleAndAudioBitrate(
        HlsRendition rendition,
        string expectedWidth,
        string expectedAudioBitrate)
    {
        HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(rendition);

        Assert.Multiple(() =>
        {
            Assert.That(plan.VideoCodecArgs, Does.Contain($"min({expectedWidth}"));
            Assert.That(plan.AudioCodecArgs, Does.Contain(expectedAudioBitrate));
        });
    }
}
