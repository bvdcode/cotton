// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Internal
{
    internal static class CottonHttpDownloadWriter
    {
        public static async Task CopyAsync(
            HttpResponseMessage response,
            Stream destination,
            string path,
            IProgress<long>? progress,
            long? expectedBodyLength,
            CancellationToken cancellationToken)
        {
            await using Stream source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            long? remaining = expectedBodyLength;
            long validatedBodyLength = expectedBodyLength.GetValueOrDefault();

            while (true)
            {
                int bufferLength = buffer.Length;
                if (remaining.HasValue)
                {
                    if (remaining.Value == 0)
                    {
                        int extraRead = await source
                            .ReadAsync(buffer.AsMemory(0, 1), cancellationToken)
                            .ConfigureAwait(false);
                        if (extraRead != 0)
                        {
                            throw CreateInvalidBodyLengthException(
                                response,
                                path,
                                validatedBodyLength,
                                longer: true);
                        }

                        break;
                    }

                    bufferLength = (int)Math.Min(buffer.Length, remaining.Value);
                }

                int read = await source
                    .ReadAsync(buffer.AsMemory(0, bufferLength), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    if (remaining.HasValue && remaining.Value > 0)
                    {
                        throw CreateInvalidBodyLengthException(
                            response,
                            path,
                            validatedBodyLength,
                            longer: false);
                    }

                    break;
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                total += read;
                if (remaining.HasValue)
                {
                    remaining -= read;
                }

                progress?.Report(total);
            }
        }

        private static CottonApiException CreateInvalidBodyLengthException(
            HttpResponseMessage response,
            string path,
            long expectedBodyLength,
            bool longer)
        {
            string direction = longer ? "more" : "fewer";
            return new CottonApiException(
                response.StatusCode,
                null,
                $"Cotton API download GET {CottonHttpResponseReader.RedactPath(path)} returned " +
                $"{direction} bytes than expected; expected {expectedBodyLength} bytes.");
        }
    }
}
