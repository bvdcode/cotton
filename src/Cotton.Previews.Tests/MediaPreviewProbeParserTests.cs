// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class MediaPreviewProbeParserTests
    {
        [Test]
        public void Parse_ContainerWithCoverAndMultipleVideos_SelectsFirstVideoTrackDuration()
        {
            const string raw = """
                {
                  "streams": [
                    { "index": 0, "codec_type": "audio", "duration": "30" },
                    { "index": 1, "codec_type": "video", "duration": "30", "disposition": { "attached_pic": 1 } },
                    { "index": 2, "codec_type": "video", "duration": "2.5", "disposition": { "attached_pic": 0 } },
                    { "index": 3, "codec_type": "video", "duration": "10" }
                  ],
                  "format": { "duration": "30" }
                }
                """;

            MediaPreviewProbeInfo result = MediaPreviewProbeParser.Parse(raw);

            Assert.Multiple(() =>
            {
                Assert.That(result.VideoStreamIndex, Is.EqualTo(2));
                Assert.That(result.VideoDurationSeconds, Is.EqualTo(2.5));
                Assert.That(result.HasAudio, Is.True);
            });
        }

        [Test]
        public void Parse_AudioWithAttachedPicture_DoesNotTreatCoverAsVideo()
        {
            const string raw = """
                { "streams": [
                  { "index": 0, "codec_type": "audio" },
                  { "index": 1, "codec_type": "video", "disposition": { "attached_pic": 1 } }
                ] }
                """;

            MediaPreviewProbeInfo result = MediaPreviewProbeParser.Parse(raw);

            Assert.Multiple(() =>
            {
                Assert.That(result.VideoStreamIndex, Is.Null);
                Assert.That(result.VideoDurationSeconds, Is.Null);
                Assert.That(result.HasAudio, Is.True);
            });
        }

        [TestCase("N/A")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void Parse_UnavailableVideoDuration_DoesNotUseContainerDuration(string duration)
        {
            string raw = $$"""
                {
                  "streams": [ { "index": 0, "codec_type": "video", "duration": "{{duration}}" } ],
                  "format": { "duration": "30" }
                }
                """;

            MediaPreviewProbeInfo result = MediaPreviewProbeParser.Parse(raw);

            Assert.That(result.VideoDurationSeconds, Is.Null);
        }
    }
}
