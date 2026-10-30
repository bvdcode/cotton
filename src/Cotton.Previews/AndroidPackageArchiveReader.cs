// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using System.IO.Compression;

namespace Cotton.Previews
{
    internal static class AndroidPackageArchiveReader
    {
        private const long MaxIconBytes = 12L * 1024 * 1024;

        public static async Task<byte[]?> TryReadBestResourceImageBytesAsync(
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
                (int Width, int Height)? dimensions = TryIdentifyImageDimensions(iconBytes);
                if (iconBytes is null || dimensions is null)
                {
                    continue;
                }

                int score = iconPath.Score
                    + AndroidPackageIconSelector.ScoreImageDimensions(dimensions.Value.Width, dimensions.Value.Height)
                    + AndroidPackageIconSelector.ScorePreviewFit(dimensions.Value.Width, dimensions.Value.Height, previewSize);
                if (score > 0 && (bestIcon is null || score > bestIcon.Score))
                {
                    bestIcon = new IconBytesCandidate(iconBytes, score);
                }
            }

            return bestIcon?.Bytes;
        }

        public static async Task<byte[]?> TryScanIconEntriesAsync(
            ZipArchive archive,
            bool requireExplicitIconName)
        {
            IconBytesCandidate? bestIcon = null;
            foreach (AndroidPackageIconEntryCandidate candidate in
                     AndroidPackageIconSelector.SelectIconEntries(archive, requireExplicitIconName))
            {
                byte[]? iconBytes = await TryReadEntryBytesAsync(candidate.Entry, MaxIconBytes).ConfigureAwait(false);
                (int Width, int Height)? dimensions = TryIdentifyImageDimensions(iconBytes);
                if (iconBytes is null || dimensions is null)
                {
                    continue;
                }

                int score = candidate.Score
                    + AndroidPackageIconSelector.ScoreImageDimensions(dimensions.Value.Width, dimensions.Value.Height);
                if (score > 0 && (bestIcon is null || score > bestIcon.Score))
                {
                    bestIcon = new IconBytesCandidate(iconBytes, score);
                }
            }

            return bestIcon?.Bytes;
        }

        public static async Task<byte[]?> TryReadEntryBytesAsync(ZipArchiveEntry entry, long maxBytes)
        {
            if (entry.Length <= 0 || entry.Length > maxBytes)
            {
                return null;
            }

            await using Stream entryStream = await entry.OpenAsync().ConfigureAwait(false);
            MemoryStream output = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
            await CopyBoundedAsync(entryStream, output, maxBytes).ConfigureAwait(false);
            return output.ToArray();
        }

        public static async Task CopyBoundedAsync(Stream source, Stream destination, long maxBytes)
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
                    throw new InvalidDataException(
                        "Android package preview source exceeds the supported preview scan limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            }

            if (destination.CanSeek)
            {
                destination.Position = 0;
            }
        }

        private static (int Width, int Height)? TryIdentifyImageDimensions(byte[]? imageBytes)
        {
            if (imageBytes is null)
            {
                return null;
            }

            try
            {
                ImageInfo? info = Image.Identify(imageBytes);
                return info is null ? null : (info.Width, info.Height);
            }
            catch
            {
                return null;
            }
        }

        private record IconBytesCandidate(byte[] Bytes, int Score);
    }
}
