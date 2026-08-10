// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class FfmpegProcessRunnerTests
    {
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await FfmpegBinary.EnsureAvailableAsync();
        }

        [Test]
        public async Task RunAsync_ValidCommand_WritesStandardOutput()
        {
            await using MemoryStream output = new();

            await FfmpegProcessRunner.RunAsync(
                CreatePngArguments(),
                output,
                TimeSpan.FromSeconds(10),
                "test image");

            Assert.That(output.Length, Is.GreaterThan(0));
        }

        [Test]
        public void RunAsync_NonZeroExit_ReportsProcessFailure()
        {
            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await FfmpegProcessRunner.RunAsync(
                    "-hide_banner -loglevel error -i cotton-input-that-does-not-exist -f null -",
                    Stream.Null,
                    TimeSpan.FromSeconds(10),
                    "missing input"));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("failed"));
                Assert.That(exception.Message, Does.Not.Contain("timed out"));
            });
        }

        [Test]
        public void RunAsync_Timeout_ReportsTimeout()
        {
            TimeoutException? exception = Assert.ThrowsAsync<TimeoutException>(
                async () => await FfmpegProcessRunner.RunAsync(
                    "-hide_banner -loglevel quiet -re -f lavfi -i anullsrc=r=8000:cl=mono -t 30 -f null -",
                    Stream.Null,
                    TimeSpan.FromMilliseconds(100),
                    "slow input"));

            Assert.That(exception!.Message, Does.Contain("timed out"));
        }

        [Test]
        public void RunAsync_OutputFailure_PreservesOriginalException()
        {
            MemoryStream output = new();
            output.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await FfmpegProcessRunner.RunAsync(
                    CreatePngArguments(),
                    output,
                    TimeSpan.FromSeconds(10),
                    "closed output"));
        }

        private static string CreatePngArguments()
        {
            return "-hide_banner -loglevel error -f lavfi -i color=c=red:s=16x16 " +
                "-frames:v 1 -f image2pipe -vcodec png pipe:1";
        }
    }
}
