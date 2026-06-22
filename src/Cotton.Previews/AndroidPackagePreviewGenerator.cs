// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;

namespace Cotton.Previews
{
    /// <summary>
    /// Extracts lightweight previews for Android package archives.
    /// </summary>
    public class AndroidPackagePreviewGenerator : IPreviewGenerator
    {
        private const int MaxEntriesToInspect = 20_000;
        private const long MaxIconBytes = 12L * 1024 * 1024;
        private const long MaxManifestBytes = 8L * 1024 * 1024;
        private const long MaxNestedPackageBytes = 192L * 1024 * 1024;
        private const long MaxNonSeekablePackageBytes = 192L * 1024 * 1024;
        private const long MaxResourceTableBytes = 32L * 1024 * 1024;
        private const int MaxNestedDepth = 1;

        /// <inheritdoc />
        public int Version => 5;

        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes => AndroidPackageContentTypes.All;

        /// <inheritdoc />
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            await using Stream? ownedStream = await CreateOwnedSeekableStreamIfNeededAsync(stream).ConfigureAwait(false);
            Stream archiveStream = ownedStream ?? stream;
            if (archiveStream.CanSeek)
            {
                archiveStream.Position = 0;
            }

            byte[]? iconBytes = await TryExtractIconBytesAsync(archiveStream, depth: 0, size).ConfigureAwait(false);
            return iconBytes is null
                ? await CreateFallbackPreviewAsync(size).ConfigureAwait(false)
                : await RenderIconPreviewAsync(iconBytes, size).ConfigureAwait(false);
        }

        private static async Task<Stream?> CreateOwnedSeekableStreamIfNeededAsync(Stream stream)
        {
            if (stream.CanSeek)
            {
                return null;
            }

            var copy = new MemoryStream();
            await CopyBoundedAsync(stream, copy, MaxNonSeekablePackageBytes).ConfigureAwait(false);
            copy.Position = 0;
            return copy;
        }

        private static async Task<byte[]?> TryExtractIconBytesAsync(Stream stream, int depth, int previewSize)
        {
            ZipArchive archive;
            try
            {
                archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            }
            catch (InvalidDataException)
            {
                return null;
            }

            using (archive)
            {
                (bool foundDeclaredIcon, byte[]? declaredIcon) =
                    await TryExtractDeclaredIconBytesAsync(archive, previewSize).ConfigureAwait(false);
                if (declaredIcon is not null)
                {
                    return declaredIcon;
                }

                byte[]? scannedIcon = await TryScanIconEntriesAsync(
                    archive,
                    requireExplicitIconName: foundDeclaredIcon).ConfigureAwait(false);
                if (scannedIcon is not null)
                {
                    return scannedIcon;
                }

                if (foundDeclaredIcon)
                {
                    return null;
                }

                if (depth >= MaxNestedDepth)
                {
                    return null;
                }

                foreach (ZipArchiveEntry nestedPackageEntry in SelectNestedPackageEntries(archive))
                {
                    byte[]? nestedBytes = await TryReadEntryBytesAsync(nestedPackageEntry, MaxNestedPackageBytes).ConfigureAwait(false);
                    if (nestedBytes is null)
                    {
                        continue;
                    }

                    using var nestedStream = new MemoryStream(nestedBytes, writable: false);
                    byte[]? nestedIcon = await TryExtractIconBytesAsync(nestedStream, depth + 1, previewSize)
                        .ConfigureAwait(false);
                    if (nestedIcon is not null)
                    {
                        return nestedIcon;
                    }
                }

                return null;
            }
        }

        private static async Task<(bool FoundDeclaredIcon, byte[]? Bytes)> TryExtractDeclaredIconBytesAsync(
            ZipArchive archive,
            int previewSize)
        {
            ZipArchiveEntry? manifestEntry = archive.GetEntry("AndroidManifest.xml");
            if (manifestEntry is null)
            {
                return (false, null);
            }

            byte[]? manifestBytes = await TryReadEntryBytesAsync(manifestEntry, MaxManifestBytes).ConfigureAwait(false);
            if (manifestBytes is null
                || !AndroidBinaryXmlApplicationIconReader.TryReadApplicationIconResourceId(
                    manifestBytes,
                    out uint iconResourceId))
            {
                return (false, null);
            }

            ZipArchiveEntry? resourceTableEntry = archive.GetEntry("resources.arsc");
            if (resourceTableEntry is null)
            {
                return (true, null);
            }

            byte[]? resourceTableBytes = await TryReadEntryBytesAsync(resourceTableEntry, MaxResourceTableBytes)
                .ConfigureAwait(false);
            if (resourceTableBytes is null)
            {
                return (true, null);
            }

            IReadOnlyList<AndroidResourcePathCandidate> iconPaths =
                AndroidResourceTableIconReader.ReadIconResourcePaths(resourceTableBytes, iconResourceId);
            byte[]? iconBytes = await TryReadBestResourceImageBytesAsync(archive, iconPaths, previewSize)
                .ConfigureAwait(false);
            if (iconBytes is not null)
            {
                return (true, iconBytes);
            }

            byte[]? adaptiveIconBytes = await TryExtractAdaptiveIconBytesAsync(
                archive,
                resourceTableBytes,
                iconResourceId,
                previewSize).ConfigureAwait(false);
            return (true, adaptiveIconBytes);
        }

        private static async Task<byte[]?> TryExtractAdaptiveIconBytesAsync(
            ZipArchive archive,
            byte[] resourceTableBytes,
            uint iconResourceId,
            int previewSize)
        {
            IReadOnlyList<AndroidResourcePathCandidate> xmlPaths =
                AndroidResourceTableIconReader.ReadXmlResourcePaths(resourceTableBytes, iconResourceId);
            foreach (AndroidResourcePathCandidate xmlPath in xmlPaths)
            {
                ZipArchiveEntry? xmlEntry = archive.GetEntry(xmlPath.Path);
                if (xmlEntry is null)
                {
                    continue;
                }

                byte[]? xmlBytes = await TryReadEntryBytesAsync(xmlEntry, MaxManifestBytes).ConfigureAwait(false);
                if (xmlBytes is null
                    || !AndroidAdaptiveIconXmlReader.TryReadLayerResourceIds(
                        xmlBytes,
                        out uint? backgroundResourceId,
                        out uint? foregroundResourceId)
                    || !foregroundResourceId.HasValue)
                {
                    continue;
                }

                IReadOnlyList<AndroidResourcePathCandidate> foregroundPaths =
                    AndroidResourceTableIconReader.ReadIconResourcePaths(
                        resourceTableBytes,
                        foregroundResourceId.Value);
                byte[]? foregroundBytes = await TryReadBestResourceImageBytesAsync(archive, foregroundPaths, previewSize)
                    .ConfigureAwait(false);
                if (foregroundBytes is null)
                {
                    continue;
                }

                byte[]? backgroundBytes = null;
                if (backgroundResourceId.HasValue)
                {
                    IReadOnlyList<AndroidResourcePathCandidate> backgroundPaths =
                        AndroidResourceTableIconReader.ReadIconResourcePaths(
                            resourceTableBytes,
                            backgroundResourceId.Value);
                    backgroundBytes = await TryReadBestResourceImageBytesAsync(archive, backgroundPaths, previewSize)
                        .ConfigureAwait(false);
                }

                return await RenderAdaptiveIconPngBytesAsync(backgroundBytes, foregroundBytes)
                    .ConfigureAwait(false);
            }

            return null;
        }

        private static async Task<byte[]?> TryReadBestResourceImageBytesAsync(
            ZipArchive archive,
            IReadOnlyList<AndroidResourcePathCandidate> iconPaths,
            int previewSize)
        {
            IconBytesCandidate? bestIcon = null;
            foreach (AndroidResourcePathCandidate iconPath in iconPaths)
            {
                ZipArchiveEntry? iconEntry = archive.GetEntry(iconPath.Path);
                if (iconEntry is null)
                {
                    continue;
                }

                byte[]? iconBytes = await TryReadEntryBytesAsync(iconEntry, MaxIconBytes).ConfigureAwait(false);
                if (iconBytes is null)
                {
                    continue;
                }

                (int Width, int Height)? dimensions = TryIdentifyImageDimensions(iconBytes);
                if (dimensions is null)
                {
                    continue;
                }

                int score = iconPath.Score
                    + ScoreImageDimensions(dimensions.Value.Width, dimensions.Value.Height)
                    + ScorePreviewFit(dimensions.Value.Width, dimensions.Value.Height, previewSize);
                if (score <= 0)
                {
                    continue;
                }

                if (bestIcon is null || score > bestIcon.Score)
                {
                    bestIcon = new IconBytesCandidate(iconBytes, score);
                }
            }

            return bestIcon?.Bytes;
        }

        private static async Task<byte[]> RenderAdaptiveIconPngBytesAsync(
            byte[]? backgroundBytes,
            byte[] foregroundBytes)
        {
            using Image<Rgba32>? background = backgroundBytes is null ? null : Image.Load<Rgba32>(backgroundBytes);
            using Image<Rgba32> foreground = Image.Load<Rgba32>(foregroundBytes);
            int canvasSize = Math.Max(foreground.Width, foreground.Height);
            if (background is not null)
            {
                canvasSize = Math.Max(canvasSize, Math.Max(background.Width, background.Height));
            }

            canvasSize = Math.Max(canvasSize, PreviewGeneratorProvider.DefaultSmallPreviewSize);
            using var canvas = new Image<Rgba32>(canvasSize, canvasSize, new Rgba32(0, 0, 0, 0));
            if (background is not null)
            {
                DrawCenteredLayer(canvas, background);
            }

            DrawCenteredLayer(canvas, foreground);

            using var output = new MemoryStream();
            await canvas.SaveAsPngAsync(output).ConfigureAwait(false);
            return output.ToArray();
        }

        private static void DrawCenteredLayer(Image<Rgba32> canvas, Image<Rgba32> source)
        {
            using Image<Rgba32> layer = source.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(canvas.Width, canvas.Height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3,
                PremultiplyAlpha = true,
            }));
            int left = (canvas.Width - layer.Width) / 2;
            int top = (canvas.Height - layer.Height) / 2;
            canvas.Mutate(x => x.DrawImage(layer, new Point(left, top), 1f));
        }

        private static async Task<byte[]?> TryScanIconEntriesAsync(
            ZipArchive archive,
            bool requireExplicitIconName)
        {
            IconBytesCandidate? bestIcon = null;
            foreach (IconEntryCandidate iconEntry in SelectIconEntries(archive, requireExplicitIconName))
            {
                byte[]? iconBytes = await TryReadEntryBytesAsync(iconEntry.Entry, MaxIconBytes).ConfigureAwait(false);
                if (iconBytes is null)
                {
                    continue;
                }

                (int Width, int Height)? dimensions = TryIdentifyImageDimensions(iconBytes);
                if (dimensions is null)
                {
                    continue;
                }

                int score = iconEntry.Score + ScoreImageDimensions(dimensions.Value.Width, dimensions.Value.Height);
                if (score <= 0)
                {
                    continue;
                }

                if (bestIcon is null || score > bestIcon.Score)
                {
                    bestIcon = new IconBytesCandidate(iconBytes, score);
                }
            }

            return bestIcon?.Bytes;
        }

        private static IEnumerable<IconEntryCandidate> SelectIconEntries(
            ZipArchive archive,
            bool requireExplicitIconName)
        {
            return archive.Entries
                .Take(MaxEntriesToInspect)
                .Select(entry => new { Entry = entry, Score = ScoreIconEntry(entry, requireExplicitIconName) })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => new IconEntryCandidate(candidate.Entry, candidate.Score));
        }

        private static IEnumerable<ZipArchiveEntry> SelectNestedPackageEntries(ZipArchive archive)
        {
            return archive.Entries
                .Take(MaxEntriesToInspect)
                .Where(entry => entry.Length > 0 && entry.Length <= MaxNestedPackageBytes)
                .Select(entry => new { Entry = entry, Score = ScoreNestedPackageEntry(entry.FullName) })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => candidate.Entry);
        }

        private static int ScoreIconEntry(
            ZipArchiveEntry entry,
            bool requireExplicitIconName)
        {
            if (entry.Length <= 0 || entry.Length > MaxIconBytes)
            {
                return 0;
            }

            string path = NormalizeEntryPath(entry.FullName);
            string extension = Path.GetExtension(path);
            if (path.EndsWith(".9.png", StringComparison.Ordinal))
            {
                return 0;
            }

            bool isKnownRasterExtension = extension is ".png" or ".webp" or ".jpg" or ".jpeg";
            bool isExtensionless = string.IsNullOrEmpty(extension);
            bool inResourceTree = path.StartsWith("res/", StringComparison.Ordinal) || path.Contains("/res/", StringComparison.Ordinal);
            if (!isKnownRasterExtension && !(isExtensionless && inResourceTree))
            {
                return 0;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            bool hasExplicitIconName = IsExplicitAppIconName(fileName);
            if (requireExplicitIconName && !hasExplicitIconName)
            {
                return 0;
            }

            bool inNamedAndroidResourceDirectory = path.StartsWith("res/mipmap", StringComparison.Ordinal)
                || path.StartsWith("res/drawable", StringComparison.Ordinal)
                || path.Contains("/res/mipmap", StringComparison.Ordinal)
                || path.Contains("/res/drawable", StringComparison.Ordinal);
            bool rootManifestIcon = !path.Contains('/', StringComparison.Ordinal)
                && hasExplicitIconName;
            if (!inNamedAndroidResourceDirectory && !inResourceTree && !rootManifestIcon)
            {
                return 0;
            }

            int score = rootManifestIcon ? 500 : 0;
            if (inNamedAndroidResourceDirectory)
            {
                score += 1_000;
            }
            else if (inResourceTree)
            {
                // Some optimized APKs obfuscate resources to extensionless paths like res/yG.
                // They can still be real PNG/WebP app icons, so try them after named resources.
                score += 2_000;
            }

            if (path.Contains("/mipmap", StringComparison.Ordinal) || path.StartsWith("res/mipmap", StringComparison.Ordinal))
            {
                score += 8_000;
            }
            else if (path.Contains("/drawable", StringComparison.Ordinal) || path.StartsWith("res/drawable", StringComparison.Ordinal))
            {
                score += 5_000;
            }

            score += ScoreIconName(fileName);
            score += ScoreDensity(path);
            score += extension is ".png" or ".webp" ? 200 : isExtensionless ? 100 : 50;
            score += (int)Math.Min(entry.Length / 1024, 512);
            return score;
        }

        private static int ScoreIconName(string fileName)
        {
            int score = 0;
            if (fileName.Contains("ic_launcher", StringComparison.Ordinal))
            {
                score += 24_000;
            }
            else if (fileName.Contains("launcher", StringComparison.Ordinal))
            {
                score += 18_000;
            }
            else if (fileName is "icon" or "app_icon" || fileName.EndsWith("_icon", StringComparison.Ordinal))
            {
                score += 14_000;
            }
            else if (fileName.Contains("logo", StringComparison.Ordinal))
            {
                score += 7_000;
            }

            if (fileName.Contains("round", StringComparison.Ordinal))
            {
                score += 1_000;
            }

            if (fileName.Contains("foreground", StringComparison.Ordinal))
            {
                score += 600;
            }

            if (fileName.Contains("background", StringComparison.Ordinal))
            {
                score -= 2_500;
            }

            if (fileName.Contains("notification", StringComparison.Ordinal))
            {
                score -= 12_000;
            }

            if (fileName.Contains("splash", StringComparison.Ordinal))
            {
                score -= 8_000;
            }

            return score;
        }

        private static int ScoreDensity(string path)
        {
            if (path.Contains("xxxhdpi", StringComparison.Ordinal))
            {
                return 600;
            }

            if (path.Contains("xxhdpi", StringComparison.Ordinal))
            {
                return 500;
            }

            if (path.Contains("xhdpi", StringComparison.Ordinal))
            {
                return 400;
            }

            if (path.Contains("hdpi", StringComparison.Ordinal))
            {
                return 300;
            }

            if (path.Contains("mdpi", StringComparison.Ordinal))
            {
                return 200;
            }

            if (path.Contains("nodpi", StringComparison.Ordinal))
            {
                return 100;
            }

            return 0;
        }

        private static int ScoreNestedPackageEntry(string entryName)
        {
            string path = NormalizeEntryPath(entryName);
            string extension = Path.GetExtension(path);
            if (extension is not ".apk" and not ".aab")
            {
                return 0;
            }

            string fileName = Path.GetFileName(path);
            int score = 100;
            if (fileName.Contains("base", StringComparison.Ordinal))
            {
                score += 1_000;
            }

            if (fileName.Contains("master", StringComparison.Ordinal))
            {
                score += 800;
            }

            return score;
        }

        private static async Task<byte[]?> TryReadEntryBytesAsync(ZipArchiveEntry entry, long maxBytes)
        {
            if (entry.Length <= 0 || entry.Length > maxBytes)
            {
                return null;
            }

            await using Stream entryStream = entry.Open();
            var output = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
            await CopyBoundedAsync(entryStream, output, maxBytes).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task CopyBoundedAsync(Stream source, Stream destination, long maxBytes)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maxBytes)
                {
                    throw new InvalidDataException("Android package preview source exceeds the supported preview scan limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            }

            if (destination.CanSeek)
            {
                destination.Position = 0;
            }
        }

        private static (int Width, int Height)? TryIdentifyImageDimensions(byte[] imageBytes)
        {
            try
            {
                var info = Image.Identify(imageBytes);
                return info is null ? null : (info.Width, info.Height);
            }
            catch
            {
                return null;
            }
        }

        private static int ScoreImageDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return -20_000;
            }

            int minSide = Math.Min(width, height);
            int maxSide = Math.Max(width, height);
            double aspectRatio = maxSide / (double)minSide;

            int score = 0;
            if (aspectRatio <= 1.15)
            {
                score += 10_000;
            }
            else if (aspectRatio <= 1.33)
            {
                score += 4_000;
            }
            else
            {
                score -= 12_000;
            }

            if (maxSide >= 192)
            {
                score += 1_500;
            }
            else if (maxSide >= 96)
            {
                score += 1_000;
            }
            else if (maxSide >= 48)
            {
                score += 500;
            }
            else
            {
                score -= 500;
            }

            if (maxSide > 2048)
            {
                score -= 4_000;
            }

            return score;
        }

        private static bool IsExplicitAppIconName(string fileName) =>
            fileName.Contains("ic_launcher", StringComparison.Ordinal)
            || fileName.Contains("launcher", StringComparison.Ordinal)
            || fileName is "icon" or "app_icon" or "logo"
            || fileName.EndsWith("_icon", StringComparison.Ordinal)
            || fileName.Contains("logo", StringComparison.Ordinal);

        private static int ScorePreviewFit(int width, int height, int previewSize)
        {
            int maxSide = Math.Max(width, height);
            int delta = Math.Abs(maxSide - previewSize);
            return Math.Max(0, 6_000 - (delta * 80));
        }

        private static async Task<byte[]> RenderIconPreviewAsync(byte[] iconBytes, int size)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(iconBytes);
            image.Mutate(x => x.AutoOrient());

            if (image.Width > size || image.Height > size)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max,
                    PremultiplyAlpha = true,
                }));
            }

            using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<byte[]> CreateFallbackPreviewAsync(int size)
        {
            int renderSize = Math.Max(size * 4, 256);
            var background = new Rgba32(18, 24, 33);
            var accent = new Rgba32(
                PreviewColorPalette.AccentGreenRed,
                PreviewColorPalette.AccentGreenGreen,
                PreviewColorPalette.AccentGreenBlue);
            var dark = new Rgba32(18, 24, 33);

            using var canvas = new Image<Rgba32>(renderSize, renderSize, background);
            DrawAndroidPackageGlyph(canvas, accent, dark);

            using Image<Rgba32> output = canvas.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3,
                PremultiplyAlpha = true,
            }));

            using var stream = new MemoryStream();
            await output.SaveAsWebpAsync(stream, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return stream.ToArray();
        }

        private static void DrawAndroidPackageGlyph(Image<Rgba32> image, Rgba32 accent, Rgba32 dark)
        {
            int s = image.Width;
            FillRect(image, s * 30 / 100, s * 25 / 100, s * 40 / 100, s * 18 / 100, accent);
            FillRect(image, s * 25 / 100, s * 45 / 100, s * 50 / 100, s * 28 / 100, accent);
            FillRect(image, s * 18 / 100, s * 48 / 100, s * 6 / 100, s * 21 / 100, accent);
            FillRect(image, s * 76 / 100, s * 48 / 100, s * 6 / 100, s * 21 / 100, accent);
            FillRect(image, s * 33 / 100, s * 73 / 100, s * 8 / 100, s * 9 / 100, accent);
            FillRect(image, s * 59 / 100, s * 73 / 100, s * 8 / 100, s * 9 / 100, accent);
            FillRect(image, s * 41 / 100, s * 32 / 100, Math.Max(2, s * 4 / 100), Math.Max(2, s * 4 / 100), dark);
            FillRect(image, s * 56 / 100, s * 32 / 100, Math.Max(2, s * 4 / 100), Math.Max(2, s * 4 / 100), dark);
            FillRect(image, s * 36 / 100, s * 18 / 100, Math.Max(2, s * 3 / 100), s * 9 / 100, accent);
            FillRect(image, s * 61 / 100, s * 18 / 100, Math.Max(2, s * 3 / 100), s * 9 / 100, accent);
        }

        private static void FillRect(Image<Rgba32> image, int left, int top, int width, int height, Rgba32 color)
        {
            int x0 = Math.Clamp(left, 0, image.Width);
            int y0 = Math.Clamp(top, 0, image.Height);
            int x1 = Math.Clamp(left + width, 0, image.Width);
            int y1 = Math.Clamp(top + height, 0, image.Height);
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }

            image.ProcessPixelRows(accessor =>
            {
                for (int y = y0; y < y1; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    row[x0..x1].Fill(color);
                }
            });
        }

        private static string NormalizeEntryPath(string path) =>
            path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

        private record IconEntryCandidate(ZipArchiveEntry Entry, int Score);

        private record IconBytesCandidate(byte[] Bytes, int Score);
    }
}
