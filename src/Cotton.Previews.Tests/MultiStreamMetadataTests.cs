// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using System.Diagnostics;

namespace Cotton.Previews.Tests
{
    public class MultiStreamMetadataTests
    {
        [Test]
        public async Task TryGetMediaMetadataAsync_VideoWithTwoAudioTracks_PreservesEveryTrack()
        {
            await FfmpegBinary.EnsureAvailableAsync();
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"metadata-tracks-{Guid.NewGuid():N}.mkv");
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = FfmpegBinary.GetFfmpegPath(),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                string[] arguments =
                [
                    "-v", "error", "-f", "lavfi", "-i", "color=c=black:s=32x24:r=2",
                    "-f", "lavfi", "-i", "anullsrc=r=8000:cl=mono", "-t", "0.5",
                    "-map", "0:v", "-map", "1:a", "-map", "1:a", "-c:v", "ffv1", "-c:a", "pcm_s16le",
                    "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=fra",
                    "-metadata", "location=+32.7-117.1/", "-metadata", "custom_label=Full metadata", "-y", path,
                ];
                foreach (string argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                using Process process = new() { StartInfo = startInfo };
                Assert.That(process.Start(), Is.True);
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                Assert.That(process.ExitCode, Is.Zero, await errorTask);

                await using FileStream stream = File.OpenRead(path);
                await using RangeStreamServer server = new(stream);
                MediaMetadataInfo metadata = (await FfmpegBinary.TryGetMediaMetadataAsync(server.Url))!;

                Assert.Multiple(() =>
                {
                    Assert.That(metadata.Properties["media.streams.0.codec_name"], Is.EqualTo("ffv1"));
                    Assert.That(metadata.Properties["media.streams.1.tags.language"], Is.EqualTo("eng"));
                    Assert.That(metadata.Properties["media.streams.2.tags.language"], Is.EqualTo("fra"));
                    Assert.That(metadata.Properties["media.streams.2.sample_rate"], Is.EqualTo("8000"));
                    Assert.That(metadata.Tags["custom_label"], Is.EqualTo("Full metadata"));
                    Assert.That(metadata.Tags["location"], Is.EqualTo("+32.7-117.1/"));
                });
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
