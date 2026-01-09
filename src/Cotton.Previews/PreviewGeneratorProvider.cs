namespace Cotton.Previews
{
    public static class PreviewGeneratorProvider
    {
        public const int DefaultPreviewSize = 150;

        private static readonly IPreviewGenerator[] Generators =
        [
            new ImagePreviewGenerator(),
            new PdfPreviewGenerator(),
            new TextPreviewGenerator(),
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

        public static ICollection<string> GetAllSupportedMimeTypes()
        {
            return GeneratorsByContentType.Keys;
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
