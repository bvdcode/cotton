// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Previews
{
    public static class PreviewGeneratorProvider
    {
        public const int DefaultGeneratorVersion = 0;

        public const int DefaultSmallPreviewSize = 200;

        public const int DefaultLargePreviewSize = 2560;

        private static readonly IPreviewGenerator[] Generators =
        [
            new PdfPreviewGenerator(),
            new HeicPreviewGenerator(),
            new RawPreviewGenerator(),
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
                .OrderBy(generator => generator.Priority)
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

        private static readonly IReadOnlyDictionary<string, int> GeneratorVersions = Generators
            .ToDictionary(generator => generator.Id, generator => generator.Version, StringComparer.Ordinal);

        public static int FailedAttemptVersion { get; } = CalculateFailedAttemptVersion();

        public static IReadOnlyDictionary<string, int> GetGeneratorVersions() => GeneratorVersions;

        public static IReadOnlyList<IPreviewGenerator> GetGeneratorsByContentTypes(IEnumerable<string> contentTypes)
        {
            HashSet<IPreviewGenerator> candidates = [];
            foreach (string contentType in contentTypes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                IPreviewGenerator? generator = GetGeneratorByContentType(contentType);
                if (generator is not null)
                {
                    candidates.Add(generator);
                }
            }

            return [.. Generators.Where(candidates.Contains).OrderBy(generator => generator.Priority)];
        }

        private static int CalculateFailedAttemptVersion()
        {
            string definition = string.Join("\n", Generators.OrderBy(generator => generator.Priority).Select(generator =>
                string.Join(",", generator.SupportedContentTypes.Order(StringComparer.Ordinal))
                + ":" + generator.Version.ToString(CultureInfo.InvariantCulture)
                + ":" + generator.Priority.ToString(CultureInfo.InvariantCulture)));
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(definition));
            return BinaryPrimitives.ReadInt32LittleEndian(hash);
        }

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
                .TryGetValue(contentType, out IPreviewGenerator? generator) ? generator : null;
        }
    }
}
