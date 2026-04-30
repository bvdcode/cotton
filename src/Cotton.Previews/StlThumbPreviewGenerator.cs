using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Cotton.Previews
{
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        public int Version => 5;
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

                F3dRenderResult renderResult = await TryRenderWithF3dAsync(modelFilePath, renderedPngPath, size).ConfigureAwait(false);
                bool rendered = renderResult.Success;
                string? renderDiagnostics = renderResult.Diagnostics;

                if (!rendered && string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedThreeMfPath = await TryNormalizeThreeMfArchiveAsync(modelFilePath).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
                    {
                        F3dRenderResult normalizedRenderResult = await TryRenderWithF3dAsync(normalizedThreeMfPath, renderedPngPath, size).ConfigureAwait(false);
                        rendered = normalizedRenderResult.Success;
                        renderDiagnostics = string.Join(" | ",
                            new[] { renderDiagnostics, normalizedRenderResult.Diagnostics }
                                .Where(x => !string.IsNullOrWhiteSpace(x)));
                    }
                }

                if (!rendered)
                {
                    throw new InvalidOperationException(
                        $"Failed to render {_modelExtension} preview with f3d. {renderDiagnostics}");
                }

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
            finally
            {
                TryDeleteFile(modelFilePath);
                TryDeleteFile(renderedPngPath);

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

        private static async Task<F3dRenderResult> TryRenderWithF3dAsync(string modelFilePath, string outputPngPath, int size)
        {
            F3dRenderResult primaryResult = await RunF3dAsync(
                modelFilePath,
                outputPngPath,
                size,
                renderingBackend: "osmesa",
                includeMaxSizeArgument: true,
                includeNoBackgroundArgument: true,
                includeVerboseDebugArgument: true).ConfigureAwait(false);

            if (primaryResult.Success)
            {
                return primaryResult;
            }

            F3dRenderResult fallbackResult = await RunF3dAsync(
                modelFilePath,
                outputPngPath,
                size,
                renderingBackend: "egl",
                includeMaxSizeArgument: false,
                includeNoBackgroundArgument: false,
                includeVerboseDebugArgument: false).ConfigureAwait(false);

            if (fallbackResult.Success)
            {
                return new F3dRenderResult(
                    true,
                    $"f3d fallback rendering succeeded after primary failure. Primary diagnostics: {primaryResult.Diagnostics}");
            }

            return new F3dRenderResult(
                false,
                $"Primary f3d render failed. {primaryResult.Diagnostics} | Fallback f3d render failed. {fallbackResult.Diagnostics}");
        }

        private static async Task<F3dRenderResult> RunF3dAsync(
            string modelFilePath,
            string outputPngPath,
            int size,
            string renderingBackend,
            bool includeMaxSizeArgument,
            bool includeNoBackgroundArgument,
            bool includeVerboseDebugArgument)
        {
            const int renderTimeoutSeconds = 20;

            try
            {
                TryDeleteFile(outputPngPath);

                using Process process = new();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "f3d",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.StartInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
                process.StartInfo.Environment["MESA_LOADER_DRIVER_OVERRIDE"] = "llvmpipe";
                process.StartInfo.Environment["GALLIUM_DRIVER"] = "llvmpipe";

                process.StartInfo.ArgumentList.Add("--no-config");
                process.StartInfo.ArgumentList.Add("--no-config");
                process.StartInfo.ArgumentList.Add($"--rendering-backend={renderingBackend}");
                if (includeVerboseDebugArgument)
                {
                    process.StartInfo.ArgumentList.Add("--verbose=debug");
                }
                process.StartInfo.ArgumentList.Add($"--rendering-backend={renderingBackend}");
                process.StartInfo.ArgumentList.Add(modelFilePath);
                process.StartInfo.ArgumentList.Add($"--resolution={size},{size}");
                if (includeMaxSizeArgument)
                {
                    process.StartInfo.ArgumentList.Add($"--max-size={PreviewGeneratorProvider.DefaultSmallPreviewSize}");
                }
                if (includeNoBackgroundArgument)
                {
                    process.StartInfo.ArgumentList.Add("--no-background");
                }

                process.Start();

                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();

                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(renderTimeoutSeconds));
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    return new F3dRenderResult(
                        false,
                        $"f3d exited with code {process.ExitCode} (backend={renderingBackend}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose-debug={includeVerboseDebugArgument}). stdout: {LimitDiagnostic(stdoutTask.Result)} stderr: {LimitDiagnostic(stderrTask.Result)}");
                }

                bool hasOutput = File.Exists(outputPngPath) && new FileInfo(outputPngPath).Length > 0;
                return hasOutput
                    ? new F3dRenderResult(true, null)
                    : new F3dRenderResult(
                        false,
                        $"f3d finished successfully but did not produce output file (backend={renderingBackend}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose-debug={includeVerboseDebugArgument}). stdout: {LimitDiagnostic(stdoutTask.Result)} stderr: {LimitDiagnostic(stderrTask.Result)}");
            }
            catch (OperationCanceledException)
            {
                return new F3dRenderResult(false, $"f3d render timed out after {renderTimeoutSeconds} seconds (backend={renderingBackend}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose-debug={includeVerboseDebugArgument}).");
            }
            catch (Exception ex)
            {
                return new F3dRenderResult(false, $"f3d render failed (backend={renderingBackend}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose-debug={includeVerboseDebugArgument}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string LimitDiagnostic(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "<empty>";
            }

            const int maxLength = 1000;
            string normalized = text.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength] + "...";
        }

        private readonly record struct F3dRenderResult(bool Success, string? Diagnostics);

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

    }
}
