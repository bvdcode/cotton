using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace Cotton.Previews
{
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        public int Version => 3;
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

            int bufferSize = checked(size * size * 4);
            byte[] rgbaBuffer = new byte[bufferSize];
            string modelFilePath = Path.Combine(Path.GetTempPath(), $"cotton-model-{Guid.NewGuid():N}{_modelExtension}");
            string? normalizedThreeMfPath = null;

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                await using (FileStream fileStream = new(
                    modelFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous))
                {
                    await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                }

                if (string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    byte[]? embeddedPreview = await TryExtractEmbeddedThreeMfThumbnailWebPAsync(modelFilePath, size).ConfigureAwait(false);
                    if (embeddedPreview is not null)
                    {
                        return embeddedPreview;
                    }
                }

                bool rendered = false;
                Exception? renderException = null;

                try
                {
                    rendered = RenderToBuffer(rgbaBuffer, size, modelFilePath);
                }
                catch (InvalidOperationException ex)
                {
                    renderException = ex;
                }

                if (!rendered && string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedThreeMfPath = await TryNormalizeThreeMfArchiveAsync(modelFilePath).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
                    {
                        try
                        {
                            rendered = RenderToBuffer(rgbaBuffer, size, normalizedThreeMfPath);
                        }
                        catch (InvalidOperationException ex)
                        {
                            renderException = ex;
                        }
                    }
                }

                if (!rendered)
                {
                    if (string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return await GenerateFallbackPreviewWebPAsync(size).ConfigureAwait(false);
                    }

                    if (renderException is not null)
                    {
                        throw renderException;
                    }

                    throw new InvalidOperationException("stl-thumb failed to render model preview.");
                }

                using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgbaBuffer, size, size);
                using var outputStream = new MemoryStream();
                await image.SaveAsWebpAsync(outputStream).ConfigureAwait(false);
                return outputStream.ToArray();
            }
            finally
            {
                TryDeleteFile(modelFilePath);

                if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
                {
                    TryDeleteFile(normalizedThreeMfPath);
                }
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

                    using Stream entryStream = entry.Open();
                    using var imageBytes = new MemoryStream();
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

        private static bool RenderToBuffer(byte[] rgbaBuffer, int size, string modelFilePath)
        {
            try
            {
                return StlThumbNative.RenderToBuffer(rgbaBuffer, (uint)size, (uint)size, modelFilePath);
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException("stl-thumb native library was not found.", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException("stl-thumb entry point render_to_buffer was not found.", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new InvalidOperationException("stl-thumb native library architecture is incompatible.", ex);
            }
        }

        private static async Task<byte[]> GenerateFallbackPreviewWebPAsync(int size)
        {
            using Image<Rgba32> image = new(size, size, new Rgba32(34, 34, 38, 255));
            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream).ConfigureAwait(false);
            return outputStream.ToArray();
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

                    await using Stream sourceEntryStream = sourceEntry.Open();
                    await using Stream normalizedEntryStream = normalizedEntry.Open();
                    await sourceEntryStream.CopyToAsync(normalizedEntryStream).ConfigureAwait(false);
                }

                return normalizedPath;
            }
            catch (InvalidDataException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }
            catch (IOException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }

        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Temporary-file cleanup failures must not hide the original render error.
            }
        }

        private static class StlThumbNative
        {
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
            [DllImport("stl_thumb", EntryPoint = "render_to_buffer", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool RenderToBuffer(
                byte[] buffer,
                uint width,
                uint height,
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
                [MarshalAs(UnmanagedType.LPUTF8Str)] string modelFilename);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        }
    }
}
