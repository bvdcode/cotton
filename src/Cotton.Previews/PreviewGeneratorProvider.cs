namespace Cotton.Previews
{
    public static class PreviewGeneratorProvider
    {
        public const int DefaultPreviewSize = 512;

        private static readonly IPreviewGenerator[] Generators =
        [
            new PdfPreviewGenerator(),
            new HeicPreviewGenerator(),
            new TextPreviewGenerator(),
            new VideoPreviewGenerator(),
            new ImagePreviewGenerator(),
        ];

        private static readonly Dictionary<string, IPreviewGenerator> GeneratorsByContentType =
            Generators
                .SelectMany(
                    g => g.SupportedContentTypes,
                    (g, ct) => new { ContentType = ct, Generator = g })
                .ToDictionary(
                    x => x.ContentType,
                    x => x.Generator,
                    StringComparer.OrdinalIgnoreCase);

        public static string[] GetAllSupportedMimeTypes()
        {
            return [.. GeneratorsByContentType.Keys];
        }

        public static IPreviewGenerator? GetGeneratorByContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }
            return GeneratorsByContentType
                .TryGetValue(contentType, out var generator) ? generator : null;
        }
    }
}
