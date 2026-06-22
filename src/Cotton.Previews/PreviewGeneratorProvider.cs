// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Central registry for preview generators and their supported MIME types.
    /// </summary>
    public static class PreviewGeneratorProvider
    {
        /// <summary>Version used for files without a matching preview generator.</summary>
        public const int DefaultGeneratorVersion = 0;
        /// <summary>Default size in pixels for small previews.</summary>
        public const int DefaultSmallPreviewSize = 200;
        /// <summary>Default size in pixels for large previews.</summary>
        public const int DefaultLargePreviewSize = 2560;

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
            new AndroidPackagePreviewGenerator(),
            new ImagePreviewGenerator(),
        ];

        private static readonly Dictionary<string, IPreviewGenerator> GeneratorsByContentType =
            Generators
                .SelectMany(
                    g => g.SupportedContentTypes,
                    (g, ct) => new { ContentType = ct, Generator = g })
                .GroupBy(
                    x => x.ContentType,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First().Generator,
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> GeneratorVersionsByContentType =
            GeneratorsByContentType
                .ToDictionary(
                    x => x.Key,
                    x => x.Value.Version,
                    StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns all MIME types that can produce previews.</summary>
        public static string[] GetAllSupportedMimeTypes()
        {
            return [.. GeneratorsByContentType.Keys];
        }

        /// <summary>Finds a generator by MIME type.</summary>
        public static IPreviewGenerator? GetGeneratorByContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }
            return GeneratorsByContentType
                .TryGetValue(contentType, out var generator) ? generator : null;
        }

        /// <summary>Returns preview generator versions keyed by MIME type.</summary>
        public static IReadOnlyDictionary<string, int> GetGeneratorVersionsByContentType()
        {
            return GeneratorVersionsByContentType;
        }
    }
}
