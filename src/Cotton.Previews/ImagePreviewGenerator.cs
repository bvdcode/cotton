// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class ImagePreviewGenerator : IPreviewGenerator
    {
        public string Id => "image";

        public int Version => 3;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes =>
            Configuration.Default.ImageFormats.SelectMany(x => x.MimeTypes);

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            DecoderOptions options = new DecoderOptions();
            if (stream.CanSeek)
            {
                stream.Position = 0;
                ImageInfo info = await Image.IdentifyAsync(stream).ConfigureAwait(false);
                if (info.Width > size || info.Height > size)
                {
                    options = new DecoderOptions { TargetSize = new Size(size, size) };
                }
                stream.Position = 0;
            }

            using Image<Rgba32> image = await Image
                .LoadAsync<Rgba32>(options, stream)
                .ConfigureAwait(false);
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
