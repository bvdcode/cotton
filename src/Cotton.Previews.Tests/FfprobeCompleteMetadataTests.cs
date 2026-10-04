// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class FfprobeCompleteMetadataTests
    {
        [Test]
        public void ParseMediaMetadata_PreservesEveryStreamChapterAndUnlistedTag()
        {
            const string raw = """
                {
                  "streams": [
                    { "codec_name": "hevc", "codec_type": "video", "width": 3840, "height": 2160,
                      "pix_fmt": "yuv420p10le", "r_frame_rate": "30000/1001", "color_transfer": "smpte2084",
                      "tags": { "rotate": "90" }, "side_data_list": [{ "rotation": -90 }] },
                    { "codec_name": "aac", "codec_type": "audio", "channels": 6, "sample_rate": "48000", "tags": { "language": "eng" } },
                    { "codec_name": "ac3", "codec_type": "audio", "tags": { "language": "fra" } },
                    { "codec_name": "subrip", "codec_type": "subtitle", "disposition": { "forced": 1 } }
                  ],
                  "chapters": [{ "id": 1, "start_time": "0.000", "end_time": "10.000", "tags": { "title": "Opening" } }],
                  "programs": [{ "program_id": 42, "tags": { "service_name": "Example" } }],
                  "format": { "filename": "http://127.0.0.1:12345/temporary", "duration": "42.5", "bit_rate": "1000000",
                    "tags": { "location": "+32.7083-117.15/", "custom": "  keep spaces  ", "empty": "", "a.b": "one", "a~1b": "two" } }
                }
                """;

            MediaMetadataInfo metadata = FfprobeJsonParser.ParseMediaMetadata(raw, MediaMetadataProbeLimits.Default)!;

            Assert.Multiple(() =>
            {
                Assert.That(metadata.Properties["media.streams.0.color_transfer"], Is.EqualTo("smpte2084"));
                Assert.That(metadata.Properties["media.streams.0.side_data_list.0.rotation"], Is.EqualTo("-90"));
                Assert.That(metadata.Properties["media.streams.1.channels"], Is.EqualTo("6"));
                Assert.That(metadata.Properties["media.streams.2.codec_name"], Is.EqualTo("ac3"));
                Assert.That(metadata.Properties["media.streams.3.disposition.forced"], Is.EqualTo("1"));
                Assert.That(metadata.Properties["media.chapters.0.tags.title"], Is.EqualTo("Opening"));
                Assert.That(metadata.Properties["media.programs.0.program_id"], Is.EqualTo("42"));
                Assert.That(metadata.Properties["media.format.tags.location"], Is.EqualTo("+32.7083-117.15/"));
                Assert.That(metadata.Properties["media.format.tags.custom"], Is.EqualTo("  keep spaces  "));
                Assert.That(metadata.Properties["media.format.tags.empty"], Is.Empty);
                Assert.That(metadata.Properties["media.format.tags.a~1b"], Is.EqualTo("one"));
                Assert.That(metadata.Properties["media.format.tags.a~01b"], Is.EqualTo("two"));
                Assert.That(metadata.Properties, Does.Not.ContainKey("media.format.filename"));
            });
        }
    }
}
