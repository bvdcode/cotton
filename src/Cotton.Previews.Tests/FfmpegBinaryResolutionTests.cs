// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    [NonParallelizable]
    public class FfmpegBinaryResolutionTests
    {
        private readonly Dictionary<string, string?> _environment = [];
        private string _directory = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"ffmpeg-paths-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            foreach (string name in new[] { "PATH", "COTTON_FFMPEG_PATH", "COTTON_FFPROBE_PATH" })
            {
                _environment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
            Environment.SetEnvironmentVariable("PATH", _directory);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<string, string?> entry in _environment)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
            _environment.Clear();
            Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public async Task EnsureAvailableAsync_BinariesOnPath_ResolvesBoth()
        {
            string ffmpegPath = CreateExecutable("ffmpeg");
            string ffprobePath = CreateExecutable("ffprobe");

            await FfmpegBinary.EnsureAvailableAsync();

            Assert.That(FfmpegBinary.GetFfmpegPath(), Is.EqualTo(ffmpegPath));
            Assert.That(FfmpegBinary.GetFfprobePath(), Is.EqualTo(ffprobePath));
        }

        [Test]
        public async Task EnsureAvailableAsync_ConfiguredPaths_ResolvesWithoutPath()
        {
            string ffmpegPath = CreateExecutable("ffmpeg");
            string ffprobePath = CreateExecutable("ffprobe");
            Environment.SetEnvironmentVariable("PATH", null);
            Environment.SetEnvironmentVariable("COTTON_FFMPEG_PATH", $" {ffmpegPath} ");
            Environment.SetEnvironmentVariable("COTTON_FFPROBE_PATH", ffprobePath);

            await FfmpegBinary.EnsureAvailableAsync();

            Assert.That(FfmpegBinary.GetFfmpegPath(), Is.EqualTo(ffmpegPath));
            Assert.That(FfmpegBinary.GetFfprobePath(), Is.EqualTo(ffprobePath));
        }

        [TestCase("ffmpeg", "COTTON_FFMPEG_PATH")]
        [TestCase("ffprobe", "COTTON_FFPROBE_PATH")]
        public async Task EnsureAvailableAsync_MissingBinary_ReportsInstallationRequirement(string binary, string setting)
        {
            CreateExecutable("ffmpeg");
            CreateExecutable("ffprobe");
            File.Delete(GetExecutablePath(binary));

            FileNotFoundException? exception = await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await FfmpegBinary.EnsureAvailableAsync());

            Assert.That(exception!.Message, Does.Contain(binary).And.Contain(setting).And.Contain("Install FFmpeg"));
        }

        [TestCase("COTTON_FFMPEG_PATH")]
        [TestCase("COTTON_FFPROBE_PATH")]
        public async Task EnsureAvailableAsync_InvalidConfiguredPath_ReportsSetting(string setting)
        {
            CreateExecutable("ffmpeg");
            CreateExecutable("ffprobe");
            string missingPath = Path.Combine(_directory, "missing");
            Environment.SetEnvironmentVariable(setting, missingPath);

            FileNotFoundException? exception = await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await FfmpegBinary.EnsureAvailableAsync());

            Assert.That(exception!.Message, Does.Contain(setting).And.Contain(missingPath));
        }

        [Test]
        public async Task EnsureAvailableAsync_Canceled_PropagatesCancellation()
        {
            CreateExecutable("ffmpeg");
            CreateExecutable("ffprobe");
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await FfmpegBinary.EnsureAvailableAsync(cancellation.Token));
        }

        private string CreateExecutable(string name)
        {
            string path = GetExecutablePath(name);
            File.WriteAllBytes(path, []);
            return path;
        }

        private string GetExecutablePath(string name) =>
            Path.Combine(_directory, OperatingSystem.IsWindows() ? $"{name}.exe" : name);
    }
}
