// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
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

        [TestCase(HlsRendition.Low, 854, 96_000)]
        [TestCase(HlsRendition.Medium, 1280, 128_000)]
        [TestCase(HlsRendition.High, 1920, 192_000)]
        [TestCase(HlsRendition.Source, null, 192_000)]
        public void Plan_UsesExpectedWidthAndAudioBitrate(
            HlsRendition rendition, int? expectedWidth, int expectedAudioBitRate)
        {
            HlsRenditionProfile.EncoderPlan plan = HlsRenditionProfile.Plan(rendition);

            Assert.Multiple(() =>
            {
                Assert.That(plan.MaximumWidth, Is.EqualTo(expectedWidth));
                Assert.That(plan.AudioBitRate, Is.EqualTo(expectedAudioBitRate));
            });
        }
    }
}
