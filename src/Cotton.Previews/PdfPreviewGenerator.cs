using Freeware;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    internal class PdfPreviewGenerator : IPreviewGenerator
    {
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256)
        {
            byte[] pngBytes = Pdf2Png.Convert(stream, 1, dpi: 150);
            using Image<Rgba32> image = Image.Load<Rgba32>(pngBytes);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max
            }));
            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream);
            return outputStream.ToArray();
        }
    }
}