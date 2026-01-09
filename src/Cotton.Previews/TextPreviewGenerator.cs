using EasyExtensions.Fonts.Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class TextPreviewGenerator : IPreviewGenerator
    {
        public IEnumerable<string> SupportedContentTypes =>
        [
            "text/plain",
            "application/xml",
        ];

        public Task<byte[]> GeneratePreviewWebPAsync(
            Stream stream,
            int size = PreviewGeneratorProvider.DefaultPreviewSize)
        {
            using Image<Rgba32> image = new(size, size);

            // фон
            image.Mutate(ctx => ctx.Fill(Color.White));

            // reader не закрывает stream
            using var reader = new StreamReader(stream, leaveOpen: true);

            const float padding = 10f;
            const float rowHeight = 20f;

            int maxRows = (int)((size - padding * 2) / rowHeight);
            if (maxRows <= 0) maxRows = 1;

            EasyExtensions.Fonts.Resources.StaticFonts.GetFontBytes(StaticFontName.Consola);
            var font = SystemFonts.CreateFont("Arial", 16);

            var textGraphicsOptions = new TextGraphicsOptions
            {
                TextOptions = new TextOptions(font)
                {
                    // ВАЖНО: wrap сейчас лучше выключить, иначе наедет на следующие строки
                    WrappingLength = 0, // no wrap
                    Origin = new PointF(padding, padding)
                }
            };

            image.Mutate(ctx =>
            {
                for (int i = 0; i < maxRows; i++)
                {
                    var line = reader.ReadLine();
                    if (line is null) break;

                    // лёгкая защита от супер-длинных строк: обрежем
                    if (line.Length > 200)
                        line = line[..200] + "…";

                    var pos = new PointF(padding, padding + i * rowHeight);
                    ctx.DrawText(textGraphicsOptions, line, font, Color.Black, pos);
                }
            });

            using var ms = new MemoryStream();
            image.SaveAsWebp(ms);
            return Task.FromResult(ms.ToArray());
        }
    }
}
