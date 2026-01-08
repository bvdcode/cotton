using Docnet.Core;
using Docnet.Core.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    internal sealed class PdfPreviewGenerator : IPreviewGenerator
    {
        private static readonly DocLib _docLib = DocLib.Instance;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            byte[] pdfBytes = await ReadAllBytesAsync(stream).ConfigureAwait(false);
            // pageIndex: 0-based
            const int pageIndex = 0;
            const int dpi = 150;
            using var docReader = _docLib.GetDocReader(pdfBytes, new PageDimensions(dpi, dpi));
            if (docReader.GetPageCount() <= pageIndex)
            {
                throw new InvalidOperationException("PDF has no pages.");
            }

            using var pageReader = docReader.GetPageReader(pageIndex);

            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();

            // BGRA (4 bytes per pixel)
            byte[] bgra = pageReader.GetImage();
            using Image<Bgra32> image = Image.LoadPixelData<Bgra32>(bgra, width, height);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max
            }));
            using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }
    }
}
