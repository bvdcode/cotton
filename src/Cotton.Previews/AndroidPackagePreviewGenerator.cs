// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;

namespace Cotton.Previews
{
    public class AndroidPackagePreviewGenerator : IPreviewGenerator
    {
        private const long MaxManifestBytes = 8L * 1024 * 1024;
        private const long MaxNonSeekablePackageBytes = 192L * 1024 * 1024;
        private const long MaxResourceTableBytes = 32L * 1024 * 1024;
        private const int MaxNestedDepth = 1;

        public int Version => 5;

        public IEnumerable<string> SupportedContentTypes => AndroidPackageContentTypes.All;

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

            MemoryStream copy = new MemoryStream();
            await AndroidPackageArchiveReader
                .CopyBoundedAsync(stream, copy, MaxNonSeekablePackageBytes)
                .ConfigureAwait(false);
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

                if (foundDeclaredIcon || depth >= MaxNestedDepth)
                {
                    return null;
                }

                return await TryExtractNestedIconBytesAsync(archive, depth, previewSize).ConfigureAwait(false);
            }
        }

        private static async Task<byte[]?> TryExtractNestedIconBytesAsync(
            ZipArchive archive,
            int depth,
            int previewSize)
        {
            foreach (ZipArchiveEntry nestedPackageEntry in AndroidPackageIconSelector.SelectNestedPackageEntries(archive))
            {
                byte[]? nestedBytes = await AndroidPackageArchiveReader
                    .TryReadEntryBytesAsync(nestedPackageEntry, AndroidPackageIconSelector.MaxNestedPackageBytes)
                    .ConfigureAwait(false);
                if (nestedBytes is null)
                {
                    continue;
                }

                using MemoryStream nestedStream = new(nestedBytes, writable: false);
                byte[]? nestedIcon = await TryExtractIconBytesAsync(nestedStream, depth + 1, previewSize)
                    .ConfigureAwait(false);
                if (nestedIcon is not null)
                {
                    return nestedIcon;
                }
            }

            return null;
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

            byte[]? manifestBytes = await AndroidPackageArchiveReader
                .TryReadEntryBytesAsync(manifestEntry, MaxManifestBytes)
                .ConfigureAwait(false);
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

            byte[]? resourceTableBytes = await AndroidPackageArchiveReader
                .TryReadEntryBytesAsync(resourceTableEntry, MaxResourceTableBytes)
                .ConfigureAwait(false);
            if (resourceTableBytes is null)
            {
                return (true, null);
            }

            IReadOnlyList<AndroidResourcePathCandidate> iconPaths =
                AndroidResourceTableIconReader.ReadIconResourcePaths(resourceTableBytes, iconResourceId);
            byte[]? iconBytes = await AndroidPackageArchiveReader
                .TryReadBestResourceImageBytesAsync(archive, iconPaths, previewSize)
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

                byte[]? xmlBytes = await AndroidPackageArchiveReader
                    .TryReadEntryBytesAsync(xmlEntry, MaxManifestBytes)
                    .ConfigureAwait(false);
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
                byte[]? foregroundBytes = await AndroidPackageArchiveReader
                    .TryReadBestResourceImageBytesAsync(archive, foregroundPaths, previewSize)
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
                    backgroundBytes = await AndroidPackageArchiveReader
                        .TryReadBestResourceImageBytesAsync(archive, backgroundPaths, previewSize)
                        .ConfigureAwait(false);
                }

                return await RenderAdaptiveIconPngBytesAsync(backgroundBytes, foregroundBytes)
                    .ConfigureAwait(false);
            }

            return null;
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
            using Image<Rgba32> canvas = new Image<Rgba32>(canvasSize, canvasSize, new Rgba32(0, 0, 0, 0));
            if (background is not null)
            {
                DrawCenteredLayer(canvas, background);
            }

            DrawCenteredLayer(canvas, foreground);

            using MemoryStream output = new MemoryStream();
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
            return await AndroidPackageArchiveReader
                .TryScanIconEntriesAsync(archive, requireExplicitIconName)
                .ConfigureAwait(false);
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

            using MemoryStream output = new MemoryStream();
            await image.SaveAsWebpAsync(output, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return output.ToArray();
        }

        private static async Task<byte[]> CreateFallbackPreviewAsync(int size)
        {
            int renderSize = Math.Max(size * 4, 256);
            Rgba32 background = new Rgba32(18, 24, 33);
            Rgba32 accent = new Rgba32(
                PreviewColorPalette.AccentGreenRed,
                PreviewColorPalette.AccentGreenGreen,
                PreviewColorPalette.AccentGreenBlue);
            Rgba32 dark = new Rgba32(18, 24, 33);

            using Image<Rgba32> canvas = new Image<Rgba32>(renderSize, renderSize, background);
            DrawAndroidPackageGlyph(canvas, accent, dark);

            using Image<Rgba32> output = canvas.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3,
                PremultiplyAlpha = true,
            }));

            using MemoryStream stream = new MemoryStream();
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

    }
}
