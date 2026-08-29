// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Cotton.Previews
{
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        public int Version => 9;

        public IEnumerable<string> SupportedContentTypes => _supportedContentTypes;

        private readonly string _modelExtension;
        private readonly string[] _supportedContentTypes;
        private const string ThreeMfExtension = ".3mf";

        public StlThumbPreviewGenerator()
            : this(".stl", ["model/stl", "application/sla", "application/vnd.ms-pki.stl"])
        {
        }

        private StlThumbPreviewGenerator(string modelExtension, string[] supportedContentTypes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelExtension);
            _modelExtension = modelExtension.StartsWith('.')
                ? modelExtension
                : "." + modelExtension;
            _supportedContentTypes = supportedContentTypes;
        }

        public static StlThumbPreviewGenerator CreateObjGenerator()
        {
            return new StlThumbPreviewGenerator(".obj", ["model/obj"]);
        }

        public static StlThumbPreviewGenerator CreateThreeMfGenerator()
        {
            return new StlThumbPreviewGenerator(
                ".3mf",
                ["model/3mf", "application/vnd.ms-package.3dmanufacturing-3dmodel+xml"]);
        }

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            string modelFilePath = Path.Combine(Path.GetTempPath(), $"cotton-model-{Guid.NewGuid():N}{_modelExtension}");
            string renderedPngPath = Path.Combine(Path.GetTempPath(), $"cotton-preview-{Guid.NewGuid():N}.png");
            string? normalizedThreeMfPath = null;

            try
            {
                await CopyInputToTempModelAsync(stream, modelFilePath).ConfigureAwait(false);
                EnsureModelFileIsNotEmpty(modelFilePath);

                byte[]? embeddedPreview = await TryExtractEmbeddedPreviewAsync(modelFilePath, size).ConfigureAwait(false);
                if (embeddedPreview is not null)
                {
                    return embeddedPreview;
                }

                normalizedThreeMfPath = await RenderPreviewPngAsync(modelFilePath, renderedPngPath, size).ConfigureAwait(false);
                return await ConvertRenderedPngToWebPAsync(renderedPngPath, size).ConfigureAwait(false);
            }
            finally
            {
                CleanupTempFiles(modelFilePath, renderedPngPath, normalizedThreeMfPath);
            }
        }

        private static async Task CopyInputToTempModelAsync(Stream stream, string modelFilePath)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            await using FileStream fileStream = new(
                modelFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        private void EnsureModelFileIsNotEmpty(string modelFilePath)
        {
            if (new FileInfo(modelFilePath).Length == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to render {_modelExtension} preview with f3d. Input file is empty.");
            }
        }

        private async Task<byte[]?> TryExtractEmbeddedPreviewAsync(string modelFilePath, int size)
        {
            return string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase)
                ? await TryExtractEmbeddedThreeMfThumbnailWebPAsync(modelFilePath, size).ConfigureAwait(false)
                : null;
        }

        private async Task<string?> RenderPreviewPngAsync(string modelFilePath, string renderedPngPath, int size)
        {
            F3dRenderResult renderResult = await F3dModelRenderer.RenderAsync(modelFilePath, renderedPngPath, size).ConfigureAwait(false);
            if (renderResult.Success)
            {
                return null;
            }

            (F3dRenderResult Result, string? NormalizedPath) normalizedRender = await TryRenderNormalizedThreeMfAsync(modelFilePath, renderedPngPath, size, renderResult.Diagnostics)
                .ConfigureAwait(false);
            if (normalizedRender.Result.Success)
            {
                return normalizedRender.NormalizedPath;
            }

            throw new InvalidOperationException(
                $"Failed to render {_modelExtension} preview with f3d. {normalizedRender.Result.Diagnostics}");
        }

        private async Task<(F3dRenderResult Result, string? NormalizedPath)> TryRenderNormalizedThreeMfAsync(
            string modelFilePath,
            string renderedPngPath,
            int size,
            string? primaryDiagnostics)
        {
            if (!string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
            {
                return (new F3dRenderResult(false, primaryDiagnostics), null);
            }

            string? normalizedPath = await TryNormalizeThreeMfArchiveAsync(modelFilePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return (new F3dRenderResult(false, primaryDiagnostics), null);
            }

            F3dRenderResult normalizedResult = await F3dModelRenderer.RenderAsync(normalizedPath, renderedPngPath, size).ConfigureAwait(false);
            return (MergeRenderDiagnostics(primaryDiagnostics, normalizedResult), normalizedPath);
        }

        private static F3dRenderResult MergeRenderDiagnostics(string? primaryDiagnostics, F3dRenderResult normalizedResult)
        {
            string diagnostics = string.Join(" | ",
                new[] { primaryDiagnostics, normalizedResult.Diagnostics }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            return normalizedResult with { Diagnostics = diagnostics };
        }

        private static async Task<byte[]> ConvertRenderedPngToWebPAsync(string renderedPngPath, int size)
        {
            await using FileStream renderedPngStream = new(
                renderedPngPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.Asynchronous);

            ImagePreviewGenerator imagePreviewGenerator = new();
            return await imagePreviewGenerator.GeneratePreviewWebPAsync(renderedPngStream, size).ConfigureAwait(false);
        }

        private static void CleanupTempFiles(string modelFilePath, string renderedPngPath, string? normalizedThreeMfPath)
        {
            PreviewTemporaryFile.TryDelete(modelFilePath);
            PreviewTemporaryFile.TryDelete(renderedPngPath);

            if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
            {
                PreviewTemporaryFile.TryDelete(normalizedThreeMfPath);
            }
        }

        private static async Task<byte[]?> TryExtractEmbeddedThreeMfThumbnailWebPAsync(string modelFilePath, int size)
        {
            try
            {
                await using FileStream modelFileStream = new(
                    modelFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                using ZipArchive archive = new(modelFileStream, ZipArchiveMode.Read, leaveOpen: false);
                string[] candidateEntries = GetThreeMfThumbnailCandidateEntryNames(archive);
                if (candidateEntries.Length == 0)
                {
                    return null;
                }

                ImagePreviewGenerator imagePreviewGenerator = new();

                foreach (string candidateEntry in candidateEntries)
                {
                    ZipArchiveEntry? entry = archive.GetEntry(candidateEntry);
                    if (entry is null || entry.Length <= 0)
                    {
                        continue;
                    }

                    await using Stream entryStream = await entry.OpenAsync().ConfigureAwait(false);
                    using MemoryStream imageBytes = new MemoryStream();
                    await entryStream.CopyToAsync(imageBytes).ConfigureAwait(false);
                    if (imageBytes.Length == 0)
                    {
                        continue;
                    }

                    imageBytes.Position = 0;
                    try
                    {
                        return await imagePreviewGenerator.GeneratePreviewWebPAsync(imageBytes, size).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Candidate image may be unsupported/corrupt. Continue with next candidate.
                    }
                }

                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string[] GetThreeMfThumbnailCandidateEntryNames(ZipArchive archive)
        {
            HashSet<string> results = new(StringComparer.OrdinalIgnoreCase);

            foreach (string target in TryReadThreeMfThumbnailTargetsFromRelationships(archive))
            {
                if (IsSupportedImageExtension(target))
                {
                    results.Add(target);
                }
            }

            foreach (string fallback in archive.Entries
                .Select(x => NormalizeZipEntryPath(x.FullName))
                .Where(IsSupportedImageExtension)
                .OrderByDescending(ScoreThreeMfImageCandidate))
            {
                results.Add(fallback);
            }

            return [.. results];
        }

        private static IEnumerable<string> TryReadThreeMfThumbnailTargetsFromRelationships(ZipArchive archive)
        {
            ZipArchiveEntry? relationshipsEntry = archive.GetEntry("_rels/.rels");
            if (relationshipsEntry is null)
            {
                yield break;
            }

            XDocument relationshipsDocument;
            try
            {
                using Stream entryStream = relationshipsEntry.Open();
                relationshipsDocument = XDocument.Load(entryStream);
            }
            catch (XmlException)
            {
                yield break;
            }

            XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            IEnumerable<XElement> relationshipNodes = relationshipsDocument
                .Descendants(relNs + "Relationship");

            foreach (XElement relationshipNode in relationshipNodes)
            {
                string? relationshipType = relationshipNode.Attribute("Type")?.Value;
                if (!IsThumbnailRelationshipType(relationshipType))
                {
                    continue;
                }

                string? relationshipTarget = relationshipNode.Attribute("Target")?.Value;
                if (string.IsNullOrWhiteSpace(relationshipTarget))
                {
                    continue;
                }

                yield return NormalizeZipEntryPath(relationshipTarget);
            }
        }

        private static string NormalizeZipEntryPath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                return string.Empty;
            }

            string normalized = Uri.UnescapeDataString(entryPath.Trim().Replace('\\', '/'));
            return normalized.TrimStart('/');
        }

        private static bool IsSupportedImageExtension(string entryPath)
        {
            string normalized = NormalizeZipEntryPath(entryPath);
            return normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".tif", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThumbnailRelationshipType(string? relationshipType)
        {
            if (string.IsNullOrWhiteSpace(relationshipType))
            {
                return false;
            }

            return relationshipType.EndsWith(
                    "/metadata/thumbnail",
                    StringComparison.OrdinalIgnoreCase)
                || relationshipType.Contains(
                    "thumbnail",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static int ScoreThreeMfImageCandidate(string entryPath)
        {
            string normalized = NormalizeZipEntryPath(entryPath);
            int score = 0;

            if (normalized.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }

            if (normalized.Contains("thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            if (normalized.Contains("cover", StringComparison.OrdinalIgnoreCase))
            {
                score += 80;
            }

            if (normalized.Contains("plate", StringComparison.OrdinalIgnoreCase))
            {
                score += 60;
            }

            if (normalized.Contains("small", StringComparison.OrdinalIgnoreCase))
            {
                score -= 20;
            }

            return score;
        }

        private static async Task<string?> TryNormalizeThreeMfArchiveAsync(string sourcePath)
        {
            string normalizedPath = Path.Combine(Path.GetTempPath(), $"cotton-model-normalized-{Guid.NewGuid():N}{ThreeMfExtension}");

            try
            {
                await using FileStream inputFileStream = new(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                using ZipArchive sourceArchive = new(inputFileStream, ZipArchiveMode.Read, leaveOpen: false);

                await using FileStream outputFileStream = new(
                    normalizedPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                using ZipArchive normalizedArchive = new(outputFileStream, ZipArchiveMode.Create, leaveOpen: false);
                foreach (ZipArchiveEntry sourceEntry in sourceArchive.Entries)
                {
                    ZipArchiveEntry normalizedEntry = normalizedArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                    normalizedEntry.LastWriteTime = sourceEntry.LastWriteTime;

                    await using Stream sourceEntryStream = await sourceEntry.OpenAsync().ConfigureAwait(false);
                    await using Stream normalizedEntryStream = await normalizedEntry.OpenAsync().ConfigureAwait(false);
                    await sourceEntryStream.CopyToAsync(normalizedEntryStream).ConfigureAwait(false);
                }

                return normalizedPath;
            }
            catch (InvalidDataException)
            {
                PreviewTemporaryFile.TryDelete(normalizedPath);
                return null;
            }
            catch (NotSupportedException)
            {
                PreviewTemporaryFile.TryDelete(normalizedPath);
                return null;
            }
            catch (IOException)
            {
                PreviewTemporaryFile.TryDelete(normalizedPath);
                return null;
            }
        }

    }
}
