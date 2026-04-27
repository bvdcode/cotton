namespace Cotton.Previews
{
    public static class PreviewGeneratorProvider
    {
        public const int DefaultGeneratorVersion = 0;
        public const int DefaultSmallPreviewSize = 200;
        public const int DefaultLargePreviewSize = 2000;

        private static readonly IPreviewGenerator[] Generators =
        [
            new PdfPreviewGenerator(),
            new HeicPreviewGenerator(),
            new StlThumbPreviewGenerator(),
            StlThumbPreviewGenerator.CreateObjGenerator(),
            StlThumbPreviewGenerator.CreateThreeMfGenerator(),
            new TextPreviewGenerator(),
            new AudioPreviewGenerator(),
            new VideoPreviewGenerator(),
            new SvgPreviewGenerator(),
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

        private static readonly Dictionary<string, int> GeneratorVersionsByContentType =
            GeneratorsByContentType
                .ToDictionary(
                    x => x.Key,
                    x => x.Value.Version,
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

        public static IReadOnlyDictionary<string, int> GetGeneratorVersionsByContentType()
        {
            return GeneratorVersionsByContentType;
        }
    }
}
