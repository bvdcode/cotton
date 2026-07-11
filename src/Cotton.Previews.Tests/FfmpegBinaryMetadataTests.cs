// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using Cotton.Previews.Tests.TestInfrastructure;
using System.Diagnostics;

namespace Cotton.Previews.Tests
{
    public class FfmpegBinaryMetadataTests
    {
        [Test]
        public async Task TryGetMediaMetadataAsync_FiltersTagsAndEnforcesOutputLimit()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            string mediaPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"ffprobe-metadata-{Guid.NewGuid():N}.mp3");

            try
            {
                await CreateTaggedAudioAsync(mediaPath);
                await using FileStream stream = File.OpenRead(mediaPath);
                await using RangeStreamServer server = new(stream);

                MediaMetadataInfo? metadata = await FfmpegBinary.TryGetMediaMetadataAsync(
                    server.Url,
                    timeout: TimeSpan.FromSeconds(15));

                Assert.That(metadata, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(metadata!.Tags["title"], Is.EqualTo("Bounded title"));
                    Assert.That(metadata.Tags["artist"], Is.EqualTo("Bounded artist"));
                    Assert.That(metadata.Tags["album_artist"], Is.EqualTo("Bounded album artist"));
                    Assert.That(metadata.Tags.ContainsKey("comment"), Is.False);
                });

                MediaMetadataProbeLimits strictLimits = new(
                    new FfprobeOutputLimits(
                        maxStandardOutputBytes: 32,
                        FfprobeOutputLimits.DefaultMaxStandardErrorBytes),
                    MediaMetadataProbeLimits.DefaultMaxTagValueBytes,
                    MediaMetadataProbeLimits.DefaultMaxTotalTagBytes);

                FfprobeOutputLimitExceededException? exception = Assert.ThrowsAsync<FfprobeOutputLimitExceededException>(
                    async () => await FfmpegBinary.TryGetMediaMetadataAsync(
                        server.Url,
                        timeout: TimeSpan.FromSeconds(15),
                        limits: strictLimits));

                Assert.Multiple(() =>
                {
                    Assert.That(exception!.OutputName, Is.EqualTo("stdout"));
                    Assert.That(exception.MaxBytes, Is.EqualTo(32));
                });
            }
            finally
            {
                File.Delete(mediaPath);
            }
        }

        [Test]
        public async Task TryGetMediaMetadataAsync_CallerCancellation_PropagatesCancellation()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            BlockingReadStream stream = new(length: 1024 * 1024);
            await using RangeStreamServer server = new(stream);
            using CancellationTokenSource cancellation = new();

            Task<MediaMetadataInfo?> probeTask = FfmpegBinary.TryGetMediaMetadataAsync(
                server.Url,
                timeout: TimeSpan.FromSeconds(15),
                cancellationToken: cancellation.Token);

            await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await probeTask);
        }

        [Test]
        public async Task TryGetMediaMetadataAsync_NoSupportedTags_ReturnsSuccessfulEmptyTagSet()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            string mediaPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"ffprobe-no-tags-{Guid.NewGuid():N}.mp3");

            try
            {
                await CreateAudioAsync(mediaPath, includeSupportedTags: false);
                await using FileStream stream = File.OpenRead(mediaPath);
                await using RangeStreamServer server = new(stream);

                MediaMetadataInfo? metadata = await FfmpegBinary.TryGetMediaMetadataAsync(
                    server.Url,
                    timeout: TimeSpan.FromSeconds(15));

                Assert.That(metadata, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(metadata!.AudioCodec, Is.EqualTo("mp3"));
                    Assert.That(metadata.DurationSeconds, Is.GreaterThan(0));
                    Assert.That(metadata.Tags, Is.Empty);
                });
            }
            finally
            {
                File.Delete(mediaPath);
            }
        }

        [Test]
        public async Task TryGetMediaMetadataAsync_Timeout_ReturnsProbeFailure()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            BlockingReadStream stream = new(length: 1024 * 1024);
            await using RangeStreamServer server = new(stream);

            MediaMetadataInfo? result = await FfmpegBinary.TryGetMediaMetadataAsync(
                server.Url,
                timeout: TimeSpan.FromMilliseconds(100));

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task TerminateProcessAsync_ActiveProcess_KillsProcessTree()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            ProcessStartInfo startInfo = new()
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            string[] arguments =
            [
                "-hide_banner",
                "-loglevel",
                "quiet",
                "-re",
                "-f",
                "lavfi",
                "-i",
                "anullsrc=r=8000:cl=mono",
                "-t",
                "30",
                "-f",
                "null",
                "-",
            ];
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.That(process.HasExited, Is.False);

            await FfmpegBinary.TerminateProcessAsync(process);

            Assert.That(process.HasExited, Is.True);
        }

        private static async Task CreateTaggedAudioAsync(string path)
        {
            await CreateAudioAsync(path, includeSupportedTags: true);
        }

        private static async Task CreateAudioAsync(string path, bool includeSupportedTags)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            List<string> arguments =
            [
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "lavfi",
                "-i",
                "anullsrc=r=8000:cl=mono",
                "-t",
                "0.1",
                "-metadata",
                "comment=must-not-be-returned",
            ];
            if (includeSupportedTags)
            {
                arguments.AddRange(
                [
                    "-metadata",
                    "title=Bounded title",
                    "-metadata",
                    "artist=Bounded artist",
                    "-metadata",
                    "album_artist=Bounded album artist",
                ]);
            }

            arguments.Add("-y");
            arguments.Add(path);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string stderr = await stderrTask;

            Assert.That(process.ExitCode, Is.EqualTo(0), stderr);
        }
    }
}
