using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class ImagePreviewGenerator : IPreviewGenerator
    {
        public IEnumerable<string> SupportedContentTypes =>
            Configuration.Default.ImageFormats.SelectMany(x => x.MimeTypes);

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = PreviewGeneratorProvider.DefaultPreviewSize)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
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