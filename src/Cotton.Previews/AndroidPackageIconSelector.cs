// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.IO.Compression;

namespace Cotton.Previews
{
    internal static class AndroidPackageIconSelector
    {
        private const int MaxEntriesToInspect = 20_000;
        private const long MaxIconBytes = 12L * 1024 * 1024;
        public const long MaxNestedPackageBytes = 192L * 1024 * 1024;

        public static IEnumerable<AndroidPackageIconEntryCandidate> SelectIconEntries(
            ZipArchive archive,
            bool requireExplicitIconName)
        {
            return archive.Entries
                .Take(MaxEntriesToInspect)
                .Select(entry => new { Entry = entry, Score = ScoreIconEntry(entry, requireExplicitIconName) })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => new AndroidPackageIconEntryCandidate(candidate.Entry, candidate.Score));
        }

        public static IEnumerable<ZipArchiveEntry> SelectNestedPackageEntries(ZipArchive archive)
        {
            return archive.Entries
                .Take(MaxEntriesToInspect)
                .Where(entry => entry.Length > 0 && entry.Length <= MaxNestedPackageBytes)
                .Select(entry => new { Entry = entry, Score = ScoreNestedPackageEntry(entry.FullName) })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => candidate.Entry);
        }

        public static int ScoreImageDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return -20_000;
            }

            int minSide = Math.Min(width, height);
            int maxSide = Math.Max(width, height);
            double aspectRatio = maxSide / (double)minSide;
            int score = aspectRatio switch
            {
                <= 1.15 => 10_000,
                <= 1.33 => 4_000,
                _ => -12_000,
            };

            score += maxSide switch
            {
                >= 192 => 1_500,
                >= 96 => 1_000,
                >= 48 => 500,
                _ => -500,
            };

            return maxSide > 2048 ? score - 4_000 : score;
        }

        public static int ScorePreviewFit(int width, int height, int previewSize)
        {
            int maxSide = Math.Max(width, height);
            int delta = Math.Abs(maxSide - previewSize);
            return Math.Max(0, 6_000 - (delta * 80));
        }

        private static int ScoreIconEntry(ZipArchiveEntry entry, bool requireExplicitIconName)
        {
            if (entry.Length <= 0 || entry.Length > MaxIconBytes)
            {
                return 0;
            }

            string path = NormalizeEntryPath(entry.FullName);
            string extension = Path.GetExtension(path);
            if (!IsSupportedIconPath(path, extension))
            {
                return 0;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            bool hasExplicitIconName = IsExplicitAppIconName(fileName);
            if (requireExplicitIconName && !hasExplicitIconName)
            {
                return 0;
            }

            int? locationScore = ScoreIconLocation(path, hasExplicitIconName);
            if (!locationScore.HasValue)
            {
                return 0;
            }

            return locationScore.Value
                + ScoreIconName(fileName)
                + ScoreDensity(path)
                + ScoreIconExtension(extension)
                + (int)Math.Min(entry.Length / 1024, 512);
        }

        private static bool IsSupportedIconPath(string path, string extension)
        {
            if (path.EndsWith(".9.png", StringComparison.Ordinal))
            {
                return false;
            }

            bool knownRasterExtension = extension is ".png" or ".webp" or ".jpg" or ".jpeg";
            return knownRasterExtension || (string.IsNullOrEmpty(extension) && IsInResourceTree(path));
        }

        private static int? ScoreIconLocation(string path, bool hasExplicitIconName)
        {
            bool resourceTree = IsInResourceTree(path);
            bool namedResourceDirectory = IsInNamedResourceDirectory(path);
            bool rootManifestIcon = !path.Contains('/', StringComparison.Ordinal) && hasExplicitIconName;
            if (!namedResourceDirectory && !resourceTree && !rootManifestIcon)
            {
                return null;
            }

            int score = rootManifestIcon ? 500 : 0;
            score += namedResourceDirectory ? 1_000 : resourceTree ? 2_000 : 0;
            score += ScoreResourceKind(path);
            return score;
        }

        private static bool IsInResourceTree(string path)
        {
            return path.StartsWith("res/", StringComparison.Ordinal)
                || path.Contains("/res/", StringComparison.Ordinal);
        }

        private static bool IsInNamedResourceDirectory(string path)
        {
            return path.StartsWith("res/mipmap", StringComparison.Ordinal)
                || path.StartsWith("res/drawable", StringComparison.Ordinal)
                || path.Contains("/res/mipmap", StringComparison.Ordinal)
                || path.Contains("/res/drawable", StringComparison.Ordinal);
        }

        private static int ScoreResourceKind(string path)
        {
            if (path.Contains("/mipmap", StringComparison.Ordinal)
                || path.StartsWith("res/mipmap", StringComparison.Ordinal))
            {
                return 8_000;
            }

            return path.Contains("/drawable", StringComparison.Ordinal)
                || path.StartsWith("res/drawable", StringComparison.Ordinal)
                ? 5_000
                : 0;
        }

        private static int ScoreIconExtension(string extension)
        {
            return extension switch
            {
                ".png" or ".webp" => 200,
                "" => 100,
                _ => 50,
            };
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
            return path switch
            {
                _ when path.Contains("xxxhdpi", StringComparison.Ordinal) => 600,
                _ when path.Contains("xxhdpi", StringComparison.Ordinal) => 500,
                _ when path.Contains("xhdpi", StringComparison.Ordinal) => 400,
                _ when path.Contains("hdpi", StringComparison.Ordinal) => 300,
                _ when path.Contains("mdpi", StringComparison.Ordinal) => 200,
                _ when path.Contains("nodpi", StringComparison.Ordinal) => 100,
                _ => 0,
            };
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

        private static bool IsExplicitAppIconName(string fileName)
        {
            return fileName.Contains("ic_launcher", StringComparison.Ordinal)
                || fileName.Contains("launcher", StringComparison.Ordinal)
                || fileName is "icon" or "app_icon" or "logo"
                || fileName.EndsWith("_icon", StringComparison.Ordinal)
                || fileName.Contains("logo", StringComparison.Ordinal);
        }

        private static string NormalizeEntryPath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        }
    }
}
