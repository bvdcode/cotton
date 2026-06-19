// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews
{
    /// <summary>
    /// Extracts lightweight previews for Android package archives.
    /// </summary>
    public sealed class AndroidPackagePreviewGenerator : IPreviewGenerator
    {
        private const int MaxEntriesToInspect = 20_000;
        private const long MaxIconBytes = 12L * 1024 * 1024;
        private const long MaxNestedPackageBytes = 192L * 1024 * 1024;
        private const long MaxNonSeekablePackageBytes = 192L * 1024 * 1024;
        private const long MaxResourceTableBytes = 32L * 1024 * 1024;
        private const int MaxNestedDepth = 1;
        private const ushort ResStringPoolType = 0x0001;
        private const ushort ResTableType = 0x0002;
        private const ushort ResTablePackageType = 0x0200;
        private const ushort ResTableTypeType = 0x0201;
        private const ushort ResourceEntryFlagComplex = 0x0001;
        private const uint ResourceEntryNoEntry = 0xFFFFFFFF;
        private const byte ResValueDataTypeString = 0x03;
        private const int StringPoolUtf8Flag = 0x00000100;

        /// <inheritdoc />
        public int Version => 2;

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

            byte[]? iconBytes = await TryExtractIconBytesAsync(archiveStream, depth: 0).ConfigureAwait(false);
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

        private static async Task<byte[]?> TryExtractIconBytesAsync(Stream stream, int depth)
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
                IReadOnlyDictionary<string, int> resourcePathScores =
                    await ReadResourcePathScoresAsync(archive).ConfigureAwait(false);
                IconBytesCandidate? bestIcon = null;
                foreach (IconEntryCandidate iconEntry in SelectIconEntries(archive, resourcePathScores))
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

                if (bestIcon is not null)
                {
                    return bestIcon.Bytes;
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
                    byte[]? nestedIcon = await TryExtractIconBytesAsync(nestedStream, depth + 1).ConfigureAwait(false);
                    if (nestedIcon is not null)
                    {
                        return nestedIcon;
                    }
                }

                return null;
            }
        }

        private static IEnumerable<IconEntryCandidate> SelectIconEntries(
            ZipArchive archive,
            IReadOnlyDictionary<string, int> resourcePathScores)
        {
            return archive.Entries
                .Take(MaxEntriesToInspect)
                .Select(entry => new { Entry = entry, Score = ScoreIconEntry(entry, resourcePathScores) })
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

        private static async Task<IReadOnlyDictionary<string, int>> ReadResourcePathScoresAsync(ZipArchive archive)
        {
            ZipArchiveEntry? resourceTableEntry = archive.GetEntry("resources.arsc");
            if (resourceTableEntry is null)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            byte[]? resourceTableBytes = await TryReadEntryBytesAsync(resourceTableEntry, MaxResourceTableBytes)
                .ConfigureAwait(false);
            return resourceTableBytes is null
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                : ReadResourcePathScores(resourceTableBytes);
        }

        private static IReadOnlyDictionary<string, int> ReadResourcePathScores(byte[] resourceTableBytes)
        {
            var scores = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!HasRange(resourceTableBytes, 0, 12) || ReadUInt16(resourceTableBytes, 0) != ResTableType)
            {
                return scores;
            }

            int tableHeaderSize = ReadUInt16(resourceTableBytes, 2);
            int tableSize = ClampChunkSize(resourceTableBytes, 0);
            if (tableSize == 0 || tableHeaderSize <= 0 || tableHeaderSize >= tableSize)
            {
                return scores;
            }

            int offset = tableHeaderSize;
            string[] globalStrings = [];
            if (HasRange(resourceTableBytes, offset, 8) && ReadUInt16(resourceTableBytes, offset) == ResStringPoolType)
            {
                globalStrings = ReadStringPool(resourceTableBytes, offset, out int stringPoolSize);
                offset += stringPoolSize;
            }

            while (HasRange(resourceTableBytes, offset, 8) && offset < tableSize)
            {
                ushort chunkType = ReadUInt16(resourceTableBytes, offset);
                int chunkSize = ClampChunkSize(resourceTableBytes, offset);
                if (chunkSize == 0)
                {
                    break;
                }

                if (chunkType == ResTablePackageType)
                {
                    AddPackageResourcePathScores(resourceTableBytes, offset, chunkSize, globalStrings, scores);
                }

                offset += chunkSize;
            }

            return scores;
        }

        private static void AddPackageResourcePathScores(
            byte[] resourceTableBytes,
            int packageOffset,
            int packageSize,
            IReadOnlyList<string> globalStrings,
            Dictionary<string, int> scores)
        {
            if (!HasRange(resourceTableBytes, packageOffset, 284))
            {
                return;
            }

            int packageHeaderSize = ReadUInt16(resourceTableBytes, packageOffset + 2);
            int packageEnd = packageOffset + packageSize;
            int typeStringsOffset = (int)ReadUInt32(resourceTableBytes, packageOffset + 268);
            int keyStringsOffset = (int)ReadUInt32(resourceTableBytes, packageOffset + 276);
            if (packageHeaderSize <= 0 || packageHeaderSize > packageSize)
            {
                return;
            }

            string[] typeStrings = ReadStringPool(resourceTableBytes, packageOffset + typeStringsOffset, out _);
            string[] keyStrings = ReadStringPool(resourceTableBytes, packageOffset + keyStringsOffset, out _);

            int offset = packageOffset + packageHeaderSize;
            while (HasRange(resourceTableBytes, offset, 8) && offset < packageEnd)
            {
                ushort chunkType = ReadUInt16(resourceTableBytes, offset);
                int chunkSize = ClampChunkSize(resourceTableBytes, offset);
                if (chunkSize == 0)
                {
                    break;
                }

                if (chunkType == ResTableTypeType)
                {
                    AddTypeResourcePathScores(resourceTableBytes, offset, typeStrings, keyStrings, globalStrings, scores);
                }

                offset += chunkSize;
            }
        }

        private static void AddTypeResourcePathScores(
            byte[] resourceTableBytes,
            int typeOffset,
            IReadOnlyList<string> typeStrings,
            IReadOnlyList<string> keyStrings,
            IReadOnlyList<string> globalStrings,
            Dictionary<string, int> scores)
        {
            if (!HasRange(resourceTableBytes, typeOffset, 20))
            {
                return;
            }

            int headerSize = ReadUInt16(resourceTableBytes, typeOffset + 2);
            int chunkSize = ClampChunkSize(resourceTableBytes, typeOffset);
            int typeId = resourceTableBytes[typeOffset + 8];
            int entryCount = (int)ReadUInt32(resourceTableBytes, typeOffset + 12);
            int entriesStart = (int)ReadUInt32(resourceTableBytes, typeOffset + 16);
            if (chunkSize == 0
                || headerSize <= 0
                || headerSize > chunkSize
                || entriesStart <= 0
                || entriesStart > chunkSize
                || entryCount < 0
                || typeId <= 0
                || typeId > typeStrings.Count)
            {
                return;
            }

            string typeName = typeStrings[typeId - 1];
            if (typeName is not ("mipmap" or "drawable"))
            {
                return;
            }

            int offsetsStart = typeOffset + headerSize;
            int entriesBase = typeOffset + entriesStart;
            if (!HasRange(resourceTableBytes, offsetsStart, entryCount * sizeof(uint)))
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                uint entryOffset = ReadUInt32(resourceTableBytes, offsetsStart + (entryIndex * sizeof(uint)));
                if (entryOffset == ResourceEntryNoEntry)
                {
                    continue;
                }

                int entryPosition = entriesBase + (int)entryOffset;
                if (!HasRange(resourceTableBytes, entryPosition, 16))
                {
                    continue;
                }

                int entrySize = ReadUInt16(resourceTableBytes, entryPosition);
                ushort flags = ReadUInt16(resourceTableBytes, entryPosition + 2);
                int keyIndex = (int)ReadUInt32(resourceTableBytes, entryPosition + 4);
                int valuePosition = entryPosition + entrySize;
                if ((flags & ResourceEntryFlagComplex) != 0
                    || keyIndex < 0
                    || keyIndex >= keyStrings.Count
                    || !HasRange(resourceTableBytes, valuePosition, 8))
                {
                    continue;
                }

                byte dataType = resourceTableBytes[valuePosition + 3];
                int stringIndex = (int)ReadUInt32(resourceTableBytes, valuePosition + 4);
                if (dataType != ResValueDataTypeString || stringIndex < 0 || stringIndex >= globalStrings.Count)
                {
                    continue;
                }

                string path = NormalizeEntryPath(globalStrings[stringIndex]);
                int score = ScoreAndroidResourceReference(typeName, keyStrings[keyIndex], path);
                if (score <= 0)
                {
                    continue;
                }

                scores[path] = scores.TryGetValue(path, out int existingScore)
                    ? Math.Max(existingScore, score)
                    : score;
            }
        }

        private static int ScoreAndroidResourceReference(string typeName, string keyName, string path)
        {
            if (!IsRasterResourcePath(path))
            {
                return 0;
            }

            string normalizedKey = keyName.ToLowerInvariant();
            int score = typeName == "mipmap" ? 50_000 : 15_000;
            if (normalizedKey is "ic_launcher" or "ic_app" or "app_icon" or "icon")
            {
                score += 50_000;
            }
            else if (normalizedKey.Contains("launcher", StringComparison.Ordinal))
            {
                score += 42_000;
            }
            else if (normalizedKey.StartsWith("ic_app", StringComparison.Ordinal))
            {
                score += 35_000;
            }
            else if (normalizedKey.Contains("icon", StringComparison.Ordinal))
            {
                score += 20_000;
            }
            else if (typeName == "mipmap")
            {
                score += 5_000;
            }
            else
            {
                return 0;
            }

            if (normalizedKey.Contains("settings", StringComparison.Ordinal))
            {
                score -= 30_000;
            }

            if (normalizedKey.Contains("notification", StringComparison.Ordinal))
            {
                score -= 50_000;
            }

            if (normalizedKey.Contains("splash", StringComparison.Ordinal))
            {
                score -= 40_000;
            }

            if (normalizedKey.Contains("background", StringComparison.Ordinal))
            {
                score -= 30_000;
            }

            return Math.Max(0, score);
        }

        private static bool IsRasterResourcePath(string path)
        {
            string extension = Path.GetExtension(path);
            return extension is ".png" or ".webp" or ".jpg" or ".jpeg";
        }

        private static int ScoreIconEntry(ZipArchiveEntry entry, IReadOnlyDictionary<string, int> resourcePathScores)
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
            bool inNamedAndroidResourceDirectory = path.StartsWith("res/mipmap", StringComparison.Ordinal)
                || path.StartsWith("res/drawable", StringComparison.Ordinal)
                || path.Contains("/res/mipmap", StringComparison.Ordinal)
                || path.Contains("/res/drawable", StringComparison.Ordinal);
            bool rootManifestIcon = !path.Contains('/', StringComparison.Ordinal)
                && (fileName is "icon" or "app_icon" or "logo" || fileName.Contains("launcher", StringComparison.Ordinal));
            if (!inNamedAndroidResourceDirectory && !inResourceTree && !rootManifestIcon)
            {
                return 0;
            }

            int score = rootManifestIcon ? 500 : 0;
            if (resourcePathScores.TryGetValue(path, out int resourceScore))
            {
                score += resourceScore;
            }

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
                }));
            }

            using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 82 }).ConfigureAwait(false);
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
            }));

            using var stream = new MemoryStream();
            await output.SaveAsWebpAsync(stream, new WebpEncoder { Quality = 82 }).ConfigureAwait(false);
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

        private static string[] ReadStringPool(byte[] data, int chunkOffset, out int chunkSize)
        {
            chunkSize = 0;
            if (!HasRange(data, chunkOffset, 28) || ReadUInt16(data, chunkOffset) != ResStringPoolType)
            {
                return [];
            }

            int headerSize = ReadUInt16(data, chunkOffset + 2);
            chunkSize = ClampChunkSize(data, chunkOffset);
            if (chunkSize == 0 || headerSize <= 0)
            {
                return [];
            }

            int stringCount = (int)ReadUInt32(data, chunkOffset + 8);
            int flags = (int)ReadUInt32(data, chunkOffset + 20);
            int stringsStart = (int)ReadUInt32(data, chunkOffset + 24);
            int offsetsStart = chunkOffset + headerSize;
            if (stringCount < 0 || !HasRange(data, offsetsStart, stringCount * sizeof(uint)))
            {
                return [];
            }

            var strings = new string[stringCount];
            bool isUtf8 = (flags & StringPoolUtf8Flag) != 0;
            int stringsBase = chunkOffset + stringsStart;
            int chunkEnd = chunkOffset + chunkSize;
            for (int i = 0; i < stringCount; i++)
            {
                int stringOffset = stringsBase + (int)ReadUInt32(data, offsetsStart + (i * sizeof(uint)));
                strings[i] = ReadStringPoolString(data, stringOffset, chunkEnd, isUtf8);
            }

            return strings;
        }

        private static string ReadStringPoolString(byte[] data, int offset, int limit, bool isUtf8)
        {
            if (offset < 0 || offset >= limit || offset >= data.Length)
            {
                return string.Empty;
            }

            int current = offset;
            if (isUtf8)
            {
                if (!TryReadStringPoolUtf8Length(data, ref current, limit, out _)
                    || !TryReadStringPoolUtf8Length(data, ref current, limit, out int byteLength)
                    || byteLength < 0
                    || current > limit - byteLength
                    || current > data.Length - byteLength)
                {
                    return string.Empty;
                }

                return Encoding.UTF8.GetString(data, current, byteLength);
            }

            if (!TryReadStringPoolUtf16Length(data, ref current, limit, out int charLength)
                || charLength < 0
                || charLength > (limit - current) / sizeof(char)
                || charLength > (data.Length - current) / sizeof(char))
            {
                return string.Empty;
            }

            return Encoding.Unicode.GetString(data, current, charLength * sizeof(char));
        }

        private static bool TryReadStringPoolUtf8Length(byte[] data, ref int offset, int limit, out int length)
        {
            length = 0;
            if (offset >= limit || offset >= data.Length)
            {
                return false;
            }

            int first = data[offset++];
            if ((first & 0x80) == 0)
            {
                length = first;
                return true;
            }

            if (offset >= limit || offset >= data.Length)
            {
                return false;
            }

            length = ((first & 0x7F) << 8) | data[offset++];
            return true;
        }

        private static bool TryReadStringPoolUtf16Length(byte[] data, ref int offset, int limit, out int length)
        {
            length = 0;
            if (!HasRange(data, offset, sizeof(ushort)) || offset > limit - sizeof(ushort))
            {
                return false;
            }

            int first = ReadUInt16(data, offset);
            offset += sizeof(ushort);
            if ((first & 0x8000) == 0)
            {
                length = first;
                return true;
            }

            if (!HasRange(data, offset, sizeof(ushort)) || offset > limit - sizeof(ushort))
            {
                return false;
            }

            length = ((first & 0x7FFF) << 16) | ReadUInt16(data, offset);
            offset += sizeof(ushort);
            return true;
        }

        private static int ClampChunkSize(byte[] data, int offset)
        {
            if (!HasRange(data, offset, 8))
            {
                return 0;
            }

            uint chunkSize = ReadUInt32(data, offset + 4);
            return chunkSize == 0 || chunkSize > int.MaxValue || chunkSize > data.Length - offset
                ? 0
                : (int)chunkSize;
        }

        private static bool HasRange(byte[] data, int offset, int length) =>
            offset >= 0 && length >= 0 && offset <= data.Length - length;

        private static ushort ReadUInt16(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));

        private static uint ReadUInt32(byte[] data, int offset) =>
            (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));

        private static string NormalizeEntryPath(string path) =>
            path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

        private sealed record IconEntryCandidate(ZipArchiveEntry Entry, int Score);

        private sealed record IconBytesCandidate(byte[] Bytes, int Score);
    }
}
