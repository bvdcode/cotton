// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class ImagePreviewGenerator : IPreviewGenerator
    {
        public int Version => 3;

        public IEnumerable<string> SupportedContentTypes =>
            Configuration.Default.ImageFormats.SelectMany(x => x.MimeTypes);

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.AutoOrient());
            return await EncodeMaxResizedWebpAsync(image, size);
        }

        public static async Task<byte[]> EncodeMaxResizedWebpAsync(Image image, int size)
        {
            if (image.Width > size || image.Height > size)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max
                }));
            }

            using MemoryStream outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream, PreviewImageEncoder.Create(size));
            return outputStream.ToArray();
        }
    }
}
