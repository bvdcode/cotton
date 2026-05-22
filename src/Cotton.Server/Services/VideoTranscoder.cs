// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using Cotton.Previews.Http;
using System.Diagnostics;
using System.Globalization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents video transcoder.
    /// </summary>
    public sealed class VideoTranscoder(ILogger<VideoTranscoder> logger)
    {
        /// <summary>
        /// Defines the segment content type.
        /// </summary>
        public const string SegmentContentType = "video/mp2t";

        private const int PipeBufferSize = 64 * 1024;

        /// <summary>
        /// Transcodes one HLS segment with ffmpeg.
        /// </summary>
        public async Task TranscodeSegmentAsync(
            Stream source,
            Stream destination,
            double startSeconds,
            double durationSeconds,
            HlsRenditionProfile.EncoderPlan encoder,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentOutOfRangeException.ThrowIfNegative(startSeconds);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);

            if (!source.CanSeek)
            {
                throw new InvalidOperationException("Video transcoding requires a seekable source stream.");
            }

            await FfmpegBinary.EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                source.Seek(0, SeekOrigin.Begin);
            }
            catch
            {
                // The source already reported CanSeek; ffmpeg range reads will fail if this is untrue.
            }

            await using var rangeServer = new RangeStreamServer(source, logger);
            string arguments = BuildSegmentArguments(rangeServer.Url, startSeconds, durationSeconds, encoder);
            await RunFfmpegAsync(arguments, destination, cancellationToken).ConfigureAwait(false);
        }

        private static string BuildSegmentArguments(
            Uri sourceUrl,
            double startSeconds,
            double durationSeconds,
            HlsRenditionProfile.EncoderPlan encoder)
        {
            string ss = startSeconds.ToString("F3", CultureInfo.InvariantCulture);
            string t = durationSeconds.ToString("F3", CultureInfo.InvariantCulture);
            string offset = startSeconds.ToString("F3", CultureInfo.InvariantCulture);

            return
                "-hide_banner -loglevel error -nostdin " +
                "-fflags +genpts " +
                $"-ss {ss} -i \"{sourceUrl}\" -t {t} " +
                encoder.CombinedArgs + " " +
                $"-output_ts_offset {offset} " +
                "-muxdelay 0 -muxpreload 0 " +
                "-f mpegts pipe:1";
        }

        private async Task RunFfmpegAsync(
            string arguments,
            Stream destination,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start ffmpeg for video transcoding.");
            }

            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await using var stdout = process.StandardOutput.BaseStream;
                await stdout.CopyToAsync(destination, PipeBufferSize, cancellationToken).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                string stderr = await ReadStderrAsync(stderrTask).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Video transcoding failed (exitCode={process.ExitCode}). stderr: {stderr}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                logger.LogDebug("Transcode cancelled by caller.");
                throw;
            }
            catch (Exception ex)
            {
                TryKill(process);
                string stderr = await ReadStderrAsync(stderrTask).ConfigureAwait(false);
                logger.LogWarning(ex, "ffmpeg pipeline failed. stderr: {Stderr}", stderr);
                throw;
            }
        }

        private static async Task<string> ReadStderrAsync(Task<string> stderrTask)
        {
            try
            {
                return await stderrTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }
}
