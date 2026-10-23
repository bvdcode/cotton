// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class PreviewStream
    {
        public static async Task CopyBoundedAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                total += read;
                if (total > maxBytes)
                {
                    throw new InvalidDataException("Source exceeds the supported preview size limit.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (destination.CanSeek)
            {
                destination.Position = 0;
            }
        }
    }
}
