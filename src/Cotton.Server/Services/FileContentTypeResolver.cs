// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using Microsoft.AspNetCore.StaticFiles;

namespace Cotton.Server.Services
{
    public static class FileContentTypeResolver
    {
        public const string DefaultContentType = "application/octet-stream";

        private static readonly FileExtensionContentTypeProvider FileExtensionContentTypeProvider = new();
        private static readonly IReadOnlyDictionary<string, (string ContentType, bool ForceContentType)> ExtensionOverrides =
            new Dictionary<string, (string ContentType, bool ForceContentType)>(StringComparer.OrdinalIgnoreCase)
            {
                [".heic"] = Override("image/heic"),
                [".heif"] = Override("image/heif"),
                [".heics"] = Override("image/heic-sequence"),
                [".heifs"] = Override("image/heif-sequence"),
                [".hif"] = Override("image/heif"),
                [".hifc"] = Override("image/heif-sequence"),
                [".avifs"] = Override("image/avif-sequence"),
                [".mov"] = Override("video/quicktime"),
                [".qt"] = Override("video/quicktime"),
                [".mkv"] = Override("video/x-matroska"),
                [".avi"] = Override("video/x-msvideo"),
                [".mka"] = Override("audio/x-matroska"),
                [".opus"] = Override("audio/opus"),
                [".flac"] = Override("audio/flac"),
                [".oga"] = Override("audio/ogg"),
                [".weba"] = Override("audio/webm"),
                [".aac"] = Override("audio/aac"),
                [".m4b"] = Override("audio/mp4"),
                [".m4p"] = Override("audio/mp4"),
                [".m4r"] = Override("audio/mp4"),
                [".md"] = Override("text/markdown"),
                [".markdown"] = Override("text/markdown"),
                [".cs"] = Override("text/plain"),
                [".csx"] = Override("text/plain"),
                [".lrc"] = Override("text/plain"),
                [".srt"] = Override("text/plain"),
                [".svg"] = Override("image/svg+xml"),
                [".svgz"] = Override("image/svg+xml"),
                [".stl"] = Override("model/stl", forceContentType: true),
                [".obj"] = Override("model/obj", forceContentType: true),
                [".3mf"] = Override("model/3mf", forceContentType: true),
                [".apk"] = Override(AndroidPackageContentTypes.Apk, forceContentType: true),
                [".aab"] = Override(AndroidPackageContentTypes.AndroidAppBundle, forceContentType: true),
                [".apks"] = Override(AndroidPackageContentTypes.Apks, forceContentType: true),
                [".xapk"] = Override(AndroidPackageContentTypes.Xapk, forceContentType: true),
                [".apkm"] = Override(AndroidPackageContentTypes.Apkm, forceContentType: true),
            };

        private static readonly IReadOnlySet<string> SourceTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".json", ".jsonc",
            ".html", ".htm", ".css", ".less", ".scss", ".sass", ".xml", ".php",
            ".phtml", ".cs", ".csx", ".lrc", ".srt", ".cpp", ".cc", ".cxx",
            ".c", ".h", ".hpp", ".razor", ".cshtml", ".md", ".markdown", ".diff",
            ".patch", ".java", ".vb", ".coffee", ".hbs", ".handlebars", ".bat", ".cmd",
            ".pug", ".jade", ".fs", ".fsi", ".fsx", ".fsscript", ".lua", ".ps1",
            ".psm1", ".psd1", ".py", ".pyw", ".pyi", ".rb", ".rbw", ".r", ".m",
            ".mm", ".go", ".rs", ".swift", ".kt", ".kts", ".sh", ".bash", ".zsh",
            ".yaml", ".yml", ".toml", ".ini", ".conf", ".cfg", ".sql", ".vue", ".svelte",
        };

        public static string Resolve(string? fileName, string? contentType)
        {
            string normalizedContentType = Normalize(contentType);
            return ResolveOverride(fileName, normalizedContentType)
                ?? ResolveProvided(fileName, normalizedContentType)
                ?? ResolveDetected(fileName)
                ?? (IsSourceTextFileName(fileName) ? "text/plain" : null)
                ?? (string.IsNullOrWhiteSpace(normalizedContentType) ? DefaultContentType : normalizedContentType);
        }

        public static bool IsSourceTextFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string name = Path.GetFileName(fileName);
            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".dockerignore", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string extension = Path.GetExtension(name);
            return !string.IsNullOrWhiteSpace(extension) && SourceTextExtensions.Contains(extension);
        }

        private static string? ResolveOverride(string? fileName, string normalizedContentType)
        {
            string extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(extension)
                && ExtensionOverrides.TryGetValue(extension, out (string ContentType, bool ForceContentType) metadata)
                && (metadata.ForceContentType
                    || string.IsNullOrWhiteSpace(normalizedContentType)
                    || string.Equals(normalizedContentType, DefaultContentType, StringComparison.OrdinalIgnoreCase)))
            {
                return metadata.ContentType;
            }

            return null;
        }

        private static string? ResolveProvided(string? fileName, string normalizedContentType)
        {
            if (string.IsNullOrWhiteSpace(normalizedContentType)
                || string.Equals(normalizedContentType, DefaultContentType, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return IsSourceTextFileName(fileName) && ShouldUseSourceTextContentType(normalizedContentType)
                ? "text/plain"
                : normalizedContentType;
        }

        private static string? ResolveDetected(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !FileExtensionContentTypeProvider.TryGetContentType(fileName, out string? detectedContentType)
                || string.IsNullOrWhiteSpace(detectedContentType))
            {
                return null;
            }

            string normalizedContentType = Normalize(detectedContentType);
            return IsSourceTextFileName(fileName) && ShouldUseSourceTextContentType(normalizedContentType)
                ? "text/plain"
                : normalizedContentType;
        }

        private static bool ShouldUseSourceTextContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)
                || string.Equals(contentType, DefaultContentType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return PreviewGeneratorProvider.GetGeneratorByContentType(contentType) is null
                && (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                    || contentType.StartsWith("application/x-", StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return string.Empty;
            }

            string normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
            return normalized switch
            {
                "video/mov" or "video/x-quicktime" => "video/quicktime",
                "video/vnd.avi" or "video/avi" or "video/msvideo" => "video/x-msvideo",
                "video/matroska" => "video/x-matroska",
                "image/x-heic" => "image/heic",
                "image/x-heif" => "image/heif",
                "audio/x-flac" => "audio/flac",
                "audio/x-wav" => "audio/wav",
                "audio/matroska" => "audio/x-matroska",
                "application/vnd.ms-pki.stl" => "model/stl",
                "application/apk" or "application/x-apk" or "application/vnd.android.package"
                    or AndroidPackageContentTypes.ApkLegacy => AndroidPackageContentTypes.Apk,
                AndroidPackageContentTypes.AndroidAppBundleLegacy => AndroidPackageContentTypes.AndroidAppBundle,
                AndroidPackageContentTypes.ApksLegacy or "application/x-apks" => AndroidPackageContentTypes.Apks,
                AndroidPackageContentTypes.XapkLegacy or "application/x-xapk" => AndroidPackageContentTypes.Xapk,
                AndroidPackageContentTypes.ApkmLegacy or "application/vnd.apkm" or "application/apkm"
                    => AndroidPackageContentTypes.Apkm,
                _ => normalized,
            };
        }

        private static (string ContentType, bool ForceContentType) Override(
            string contentType,
            bool forceContentType = false)
        {
            return (contentType, forceContentType);
        }
    }
}
