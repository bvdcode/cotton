using HeyRed.ImageSharp.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/heic",
            "image/heic-sequence",
            "image/heif",
            "image/heif-sequence"
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = PreviewGeneratorProvider.DefaultPreviewSize)
        {
            var options = new HeifDecoderOptions();
            using Image<Rgba32> image = HeifDecoder.Instance.Decode<Rgba32>(options, stream);
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
