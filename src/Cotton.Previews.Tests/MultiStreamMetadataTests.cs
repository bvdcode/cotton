// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class MultiStreamMetadataTests
    {
        [Test]
        public async Task ReadAsync_VideoWithTwoAudioTracks_PreservesEveryTrack()
        {
            await using FileStream stream = OpenFixture("multiple-tracks.mkv");
            MediaMetadataInfo metadata = await MediaMetadataReader.ReadAsync(stream, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.Properties.GetValueOrDefault("media.streams.0.codec_name"), Is.EqualTo("h264"));
                Assert.That(metadata.Properties.GetValueOrDefault("media.streams.1.tags.language"), Is.EqualTo("eng"));
                Assert.That(metadata.Properties.GetValueOrDefault("media.streams.2.tags.language"), Is.EqualTo("fra"));
                Assert.That(metadata.Properties.GetValueOrDefault("media.streams.2.sample_rate"), Is.EqualTo("8000"));
                Assert.That(metadata.Tags, Does.Not.ContainKey("custom_label"));
                Assert.That(stream.CanRead, Is.True);
            });
        }

        [TestCase("mulaw.wav", "pcm_mulaw")]
        [TestCase("ms-adpcm.wav", "adpcm_ms")]
        public async Task ReadAsync_CompressedWave_ReadsHeaderMetadata(string fixture, string codec)
        {
            await using FileStream stream = OpenFixture(fixture);
            MediaMetadataInfo metadata = await MediaMetadataReader.ReadAsync(stream, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.AudioCodec, Is.EqualTo(codec));
                Assert.That(metadata.Properties["media.streams.0.sample_rate"], Is.EqualTo("8000"));
                Assert.That(metadata.Properties["media.streams.0.channels"], Is.EqualTo("1"));
                Assert.That(metadata.DurationSeconds, Is.EqualTo(0.25));
            });
        }

        [Test]
        public async Task ReadAsync_Mp4DisplayMatrix_PreservesRotationDirection()
        {
            await using FileStream stream = OpenFixture("rotated-video.mp4");
            MediaMetadataInfo metadata = await MediaMetadataReader.ReadAsync(stream, CancellationToken.None);

            Assert.That(metadata.Properties.GetValueOrDefault("media.streams.0.side_data_list.0.rotation"), Is.EqualTo("90"));
        }

        private static FileStream OpenFixture(string name) =>
            File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Media", name));
    }
}
