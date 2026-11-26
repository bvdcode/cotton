// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana.Core.Metadata;

namespace Cotton.Previews.Tests
{
    public class MediaMetadataTagCollectorTests
    {
        [Test]
        public void Collect_ExcludesOversizedUnselectedTags()
        {
            MediaMetadataTagCollector collector = new();
            MediaTag[] tags = [new("id3v2", "TIT2", "Example"), new("id3v2", "private", new string('x', 10_000))];

            Dictionary<string, string> result = collector.Collect(tags, null, MediaMetadataFields.FormatTags);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["title"], Is.EqualTo("Example"));
        }

        [TestCase("TRACKNUMBER", "track")]
        [TestCase("DISCNUMBER", "disc")]
        public void Collect_NormalizesVorbisCommentNumbers(string name, string expected)
        {
            MediaMetadataTagCollector collector = new();
            MediaTag[] tags = [new("vorbis", name, "2/5")];

            Dictionary<string, string> result = collector.Collect(tags, null, MediaMetadataFields.FormatTags);

            Assert.That(result.GetValueOrDefault(expected), Is.EqualTo("2/5"));
        }

        [Test]
        public void Collect_RejectsOversizedSelectedTag()
        {
            MediaMetadataTagCollector collector = new();
            MediaTag[] tags = [new("id3v2", "TIT2", new string('x', 4_097))];

            Assert.That(() => collector.Collect(tags, null, MediaMetadataFields.FormatTags),
                Throws.TypeOf<MediaMetadataLimitExceededException>());
        }
    }
}
