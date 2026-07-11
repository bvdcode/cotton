// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class FfprobeJsonParserTests
    {
        [Test]
        public void ParseMediaMetadata_ValidPayload_ReturnsExpectedFields()
        {
            const string raw = """
                {
                  "streams": [
                    { "codec_name": "h264", "codec_type": "video", "width": 1920, "height": 1080 },
                    { "codec_name": "aac", "codec_type": "audio" }
                  ],
                  "format": {
                    "duration": "42.5",
                    "tags": { "title": " Song ", "artist": "Artist" }
                  }
                }
                """;

            MediaMetadataInfo? result = FfprobeJsonParser.ParseMediaMetadata(
                raw,
                MediaMetadataProbeLimits.Default);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.DurationSeconds, Is.EqualTo(42.5));
                Assert.That(result.VideoCodec, Is.EqualTo("h264"));
                Assert.That(result.AudioCodec, Is.EqualTo("aac"));
                Assert.That(result.Width, Is.EqualTo(1920));
                Assert.That(result.Height, Is.EqualTo(1080));
                Assert.That(result.Tags["title"], Is.EqualTo("Song"));
                Assert.That(result.Tags["artist"], Is.EqualTo("Artist"));
            });
        }

        [Test]
        public void ParseMediaMetadata_AllSupportedTags_PreservesBoundedValues()
        {
            const string raw = """
                {
                  "format": {
                    "tags": {
                      "title": "Title",
                      "artist": "Artist",
                      "album": "Album",
                      "album_artist": "Album artist",
                      "albumartist": "Albumartist",
                      "album artist": "Album artist with space",
                      "composer": "Composer",
                      "performer": "Performer",
                      "track": "1/10",
                      "tracknumber": "1",
                      "track_number": "01",
                      "disc": "1/2",
                      "discnumber": "1",
                      "disc_number": "01",
                      "date": "2026-07-10",
                      "creation_time": "2026-07-10T00:00:00Z",
                      "year": "2026",
                      "genre": "Test"
                    }
                  }
                }
                """;

            MediaMetadataInfo? result = FfprobeJsonParser.ParseMediaMetadata(
                raw,
                MediaMetadataProbeLimits.Default);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.Tags, Has.Count.EqualTo(18));
                Assert.That(result.Tags["album artist"], Is.EqualTo("Album artist with space"));
                Assert.That(result.Tags["creation_time"], Is.EqualTo("2026-07-10T00:00:00Z"));
                Assert.That(result.Tags["genre"], Is.EqualTo("Test"));
            });
        }

        [Test]
        public void ParseMediaMetadata_NoTags_ReturnsSuccessfulEmptyTagSet()
        {
            const string raw = """
                { "format": { "duration": "1.5" }, "streams": [] }
                """;

            MediaMetadataInfo? result = FfprobeJsonParser.ParseMediaMetadata(
                raw,
                MediaMetadataProbeLimits.Default);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.DurationSeconds, Is.EqualTo(1.5));
                Assert.That(result.Tags, Is.Empty);
            });
        }

        [Test]
        public void ParseMediaMetadata_IndividualTagExceedsLimit_Throws()
        {
            const string raw = """
                { "format": { "tags": { "title": "12345" } } }
                """;
            MediaMetadataProbeLimits limits = new(
                FfprobeOutputLimits.Default,
                maxTagValueBytes: 4,
                maxTotalTagBytes: 8);

            MediaMetadataLimitExceededException? exception = Assert.Throws<MediaMetadataLimitExceededException>(
                () => FfprobeJsonParser.ParseMediaMetadata(raw, limits));

            Assert.That(exception!.LimitName, Is.EqualTo("individual tag value"));
        }

        [Test]
        public void ParseMediaMetadata_AggregateTagsExceedLimit_Throws()
        {
            const string raw = """
                { "format": { "tags": { "a": "1234", "b": "5678" } } }
                """;
            MediaMetadataProbeLimits limits = new(
                FfprobeOutputLimits.Default,
                maxTagValueBytes: 4,
                maxTotalTagBytes: 8);

            MediaMetadataLimitExceededException? exception = Assert.Throws<MediaMetadataLimitExceededException>(
                () => FfprobeJsonParser.ParseMediaMetadata(raw, limits));

            Assert.That(exception!.LimitName, Is.EqualTo("aggregate tag payload"));
        }

        [Test]
        public void ParseMediaMetadata_InvalidJson_ReturnsNull()
        {
            MediaMetadataInfo? result = FfprobeJsonParser.ParseMediaMetadata(
                "not json",
                MediaMetadataProbeLimits.Default);

            Assert.That(result, Is.Null);
        }
    }
}
