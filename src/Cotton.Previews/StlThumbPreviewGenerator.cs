// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates previews for mesh and 3D model files through stl-thumb.
    /// </summary>
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        /// <inheritdoc />
        public int Version => 7;
        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes => _supportedContentTypes;

        private readonly string _modelExtension;
        private readonly string[] _supportedContentTypes;
        private const string ThreeMfExtension = ".3mf";

        /// <summary>Initializes the STL generator variant.</summary>
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

        /// <summary>Creates a generator variant for OBJ models.</summary>
        public static StlThumbPreviewGenerator CreateObjGenerator()
        {
            return new StlThumbPreviewGenerator(".obj", ["model/obj"]);
        }

        /// <summary>Creates a generator variant for 3MF models.</summary>
        public static StlThumbPreviewGenerator CreateThreeMfGenerator()
        {
            return new StlThumbPreviewGenerator(
                ".3mf",
                ["model/3mf", "application/vnd.ms-package.3dmanufacturing-3dmodel+xml"]);
        }

        /// <inheritdoc />
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
            F3dRenderResult renderResult = await TryRenderWithF3dAsync(modelFilePath, renderedPngPath, size).ConfigureAwait(false);
            if (renderResult.Success)
            {
                return null;
            }

            var normalizedRender = await TryRenderNormalizedThreeMfAsync(modelFilePath, renderedPngPath, size, renderResult.Diagnostics)
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

            F3dRenderResult normalizedResult = await TryRenderWithF3dAsync(normalizedPath, renderedPngPath, size).ConfigureAwait(false);
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
            TryDeleteFile(modelFilePath);
            TryDeleteFile(renderedPngPath);

            if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
            {
                TryDeleteFile(normalizedThreeMfPath);
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
                includeMaxSizeArgument: true,
                includeNoBackgroundArgument: true,
                includeVerboseArgument: true).ConfigureAwait(false);

            if (primaryResult.Success)
            {
                return primaryResult;
            }

            F3dRenderResult fallbackResult = await RunF3dAsync(
                modelFilePath,
                outputPngPath,
                size,
                includeMaxSizeArgument: false,
                includeNoBackgroundArgument: false,
                includeVerboseArgument: false).ConfigureAwait(false);

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
            bool includeMaxSizeArgument,
            bool includeNoBackgroundArgument,
            bool includeVerboseArgument)
        {
            const int renderTimeoutSeconds = 20;

            try
            {
                TryDeleteFile(outputPngPath);

                bool useXvfb = ShouldUseXvfb();
                using Process process = new();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = useXvfb ? "xvfb-run" : "f3d",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.StartInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
                process.StartInfo.Environment["MESA_LOADER_DRIVER_OVERRIDE"] = "llvmpipe";
                process.StartInfo.Environment["GALLIUM_DRIVER"] = "llvmpipe";

                if (useXvfb)
                {
                    process.StartInfo.ArgumentList.Add("-a");
                    process.StartInfo.ArgumentList.Add("-s");
                    process.StartInfo.ArgumentList.Add($"-screen 0 {size}x{size}x24");
                    process.StartInfo.ArgumentList.Add("f3d");
                }

                // In f3d 2.x, --dry-run disables reading the configuration file.
                process.StartInfo.ArgumentList.Add("--dry-run");
                if (includeVerboseArgument)
                {
                    process.StartInfo.ArgumentList.Add("--verbose");
                }
                process.StartInfo.ArgumentList.Add($"--input={modelFilePath}");
                process.StartInfo.ArgumentList.Add($"--output={outputPngPath}");
                process.StartInfo.ArgumentList.Add($"--resolution={size},{size}");
                process.StartInfo.ArgumentList.Add($"--color={PreviewColorPalette.AccentGreenHex}");
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
                        $"f3d exited with code {process.ExitCode} (xvfb={useXvfb}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose={includeVerboseArgument}). stdout: {LimitDiagnostic(stdoutTask.Result)} stderr: {LimitDiagnostic(stderrTask.Result)}");
                }

                bool hasOutput = File.Exists(outputPngPath) && new FileInfo(outputPngPath).Length > 0;
                return hasOutput
                    ? new F3dRenderResult(true, null)
                    : new F3dRenderResult(
                        false,
                        $"f3d finished successfully but did not produce output file (xvfb={useXvfb}, max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose={includeVerboseArgument}). stdout: {LimitDiagnostic(stdoutTask.Result)} stderr: {LimitDiagnostic(stderrTask.Result)}");
            }
            catch (OperationCanceledException)
            {
                return new F3dRenderResult(false, $"f3d render timed out after {renderTimeoutSeconds} seconds (max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose={includeVerboseArgument}).");
            }
            catch (Exception ex)
            {
                return new F3dRenderResult(false, $"f3d render failed (max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, verbose={includeVerboseArgument}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool ShouldUseXvfb()
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && IsExecutableOnPath("xvfb-run");
        }

        private static bool IsExecutableOnPath(string fileName)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }

            return false;
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
