// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    internal class SeekableReadStream(Stream stream, bool ownsStream) : IAsyncDisposable
    {
        public Stream Stream { get; } = stream;

        public static async Task<SeekableReadStream> OpenAsync(
            Stream source,
            CancellationToken cancellationToken)
        {
            if (source.CanSeek)
            {
                return new SeekableReadStream(source, false);
            }

            string path = Path.Combine(Path.GetTempPath(), $"cotton-text-{Guid.NewGuid():N}.tmp");
            FileStream temporary = new(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            bool completed = false;
            try
            {
                await source.CopyToAsync(temporary, cancellationToken);
                temporary.Position = 0;
                completed = true;
                return new SeekableReadStream(temporary, true);
            }
            finally
            {
                if (!completed)
                {
                    await temporary.DisposeAsync();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (ownsStream)
            {
                await Stream.DisposeAsync();
            }
        }
    }
}
