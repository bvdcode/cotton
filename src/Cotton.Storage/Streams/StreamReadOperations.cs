// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Streams
{
    internal static class StreamReadOperations
    {
        public static void SkipBytes(Stream stream, long count)
        {
            if (count <= 0)
            {
                return;
            }

            byte[] buffer = new byte[(int)Math.Min(81920, count)];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = stream.Read(buffer, 0, toRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while skipping bytes");
                }
                remaining -= read;
            }
        }

        public static async ValueTask SkipBytesAsync(Stream stream, long count, CancellationToken cancellationToken)
        {
            if (count <= 0)
            {
                return;
            }

            byte[] buffer = new byte[(int)Math.Min(81920, count)];
            long remaining = count;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while skipping bytes");
                }
                remaining -= read;
            }
        }
    }
}
