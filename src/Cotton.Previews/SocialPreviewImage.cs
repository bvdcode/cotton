// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public static class SocialPreviewImage
    {
        public const int MaxSize = 1200;
        public const int EncodingVersion = 1;
        private const int Quality = 85;
        private const int ConcurrencyLimit = 2;
        private static readonly SemaphoreSlim _gate = new(ConcurrencyLimit, ConcurrencyLimit);

        public static async Task<MemoryStream> EncodeJpegAsync(Stream preview, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                DecoderOptions options = new() { SkipMetadata = true, MaxFrames = 1 };
                using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(options, preview, ct);
                if (image.Width > MaxSize || image.Height > MaxSize)
                {
                    image.Mutate(context => context.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxSize, MaxSize),
                        Mode = ResizeMode.Max,
                    }));
                }
                image.Mutate(context => context.BackgroundColor(Color.White));
                MemoryStream output = new();
                try
                {
                    await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = Quality, SkipMetadata = true }, ct);
                    output.Position = 0;
                    return output;
                }
                catch
                {
                    await output.DisposeAsync();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
