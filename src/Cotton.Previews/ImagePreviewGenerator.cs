using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    internal class ImagePreviewGenerator : IPreviewGenerator
    {
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop
            }));
            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream);
            return outputStream.ToArray();
        }
    }
}