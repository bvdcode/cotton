// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates file manifest.
    /// </summary>
    public class FileManifestService(CottonDbContext _dbContext)
    {
        /// <summary>
        /// Defines the default content type.
        /// </summary>
        public const string DefaultContentType = "application/octet-stream";
        private static readonly FileExtensionContentTypeProvider fileExtensionContentTypeProvider = new();
        private static readonly IReadOnlyDictionary<string, string> extensionContentTypeOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".heic"] = "image/heic",
                [".heif"] = "image/heif",
                [".heics"] = "image/heic-sequence",
                [".heifs"] = "image/heif-sequence",
                [".hif"] = "image/heif",
                [".hifc"] = "image/heif-sequence",
                [".avifs"] = "image/avif-sequence",

                [".mov"] = "video/quicktime",
                [".qt"] = "video/quicktime",
                [".mkv"] = "video/x-matroska",
                [".avi"] = "video/x-msvideo",
                [".mka"] = "audio/x-matroska",

                [".opus"] = "audio/opus",
                [".flac"] = "audio/flac",
                [".oga"] = "audio/ogg",
                [".weba"] = "audio/webm",
                [".aac"] = "audio/aac",
                [".m4b"] = "audio/mp4",
                [".m4p"] = "audio/mp4",
                [".m4r"] = "audio/mp4",

                [".md"] = "text/markdown",
                [".markdown"] = "text/markdown",
                [".cs"] = "text/plain",
                [".csx"] = "text/plain",
                [".lrc"] = "text/plain",
                [".srt"] = "text/plain",

                [".svg"] = "image/svg+xml",
                [".svgz"] = "image/svg+xml",

                [".stl"] = "model/stl",
                [".obj"] = "model/obj",
                [".3mf"] = "model/3mf",
            };

        private static readonly IReadOnlySet<string> sourceTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ts",
            ".tsx",
            ".js",
            ".jsx",
            ".mjs",
            ".cjs",
            ".json",
            ".jsonc",
            ".html",
            ".htm",
            ".css",
            ".less",
            ".scss",
            ".sass",
            ".xml",
            ".php",
            ".phtml",
            ".cs",
            ".csx",
            ".lrc",
            ".srt",
            ".cpp",
            ".cc",
            ".cxx",
            ".c",
            ".h",
            ".hpp",
            ".razor",
            ".cshtml",
            ".md",
            ".markdown",
            ".diff",
            ".patch",
            ".java",
            ".vb",
            ".coffee",
            ".hbs",
            ".handlebars",
            ".bat",
            ".cmd",
            ".pug",
            ".jade",
            ".fs",
            ".fsi",
            ".fsx",
            ".fsscript",
            ".lua",
            ".ps1",
            ".psm1",
            ".psd1",
            ".py",
            ".pyw",
            ".pyi",
            ".rb",
            ".rbw",
            ".r",
            ".m",
            ".mm",
            ".go",
            ".rs",
            ".swift",
            ".kt",
            ".kts",
            ".sh",
            ".bash",
            ".zsh",
            ".yaml",
            ".yml",
            ".toml",
            ".ini",
            ".conf",
            ".cfg",
            ".sql",
            ".vue",
            ".svelte",
        };

        /// <summary>Regex pattern matching filenames that Cotton treats as source-code text previews.</summary>
        public static string SourceTextFileNameRegexPattern { get; } = BuildSourceTextFileNameRegexPattern();

        /// <summary>
        /// Resolves content type.
        /// </summary>
        public static string ResolveContentType(string? fileName, string? contentType)
        {
            string normalizedContentType = NormalizeContentType(contentType);
            string extension = Path.GetExtension(fileName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(extension)
                && extensionContentTypeOverrides.TryGetValue(extension, out string? overriddenContentType)
                && (ShouldForceExtensionContentType(extension)
                    || string.IsNullOrWhiteSpace(normalizedContentType)
                    || string.Equals(normalizedContentType, DefaultContentType, StringComparison.OrdinalIgnoreCase)))
            {
                return overriddenContentType;
            }

            if (!string.IsNullOrWhiteSpace(normalizedContentType)
                && !string.Equals(normalizedContentType, DefaultContentType, StringComparison.OrdinalIgnoreCase))
            {
                return IsSourceTextFileName(fileName) && ShouldUseSourceTextContentType(normalizedContentType)
                    ? "text/plain"
                    : normalizedContentType;
            }

            if (!string.IsNullOrWhiteSpace(fileName)
                && fileExtensionContentTypeProvider.TryGetContentType(fileName, out string? detectedContentType)
                && !string.IsNullOrWhiteSpace(detectedContentType))
            {
                string normalizedDetectedContentType = NormalizeContentType(detectedContentType);
                return IsSourceTextFileName(fileName) && ShouldUseSourceTextContentType(normalizedDetectedContentType)
                    ? "text/plain"
                    : normalizedDetectedContentType;
            }

            if (IsSourceTextFileName(fileName))
            {
                return "text/plain";
            }

            return string.IsNullOrWhiteSpace(normalizedContentType)
                ? DefaultContentType
                : normalizedContentType;
        }

        /// <summary>Returns true when the filename should be treated as source-code text for preview generation.</summary>
        public static bool IsSourceTextFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string name = Path.GetFileName(fileName);
            if (IsDockerfileName(name))
            {
                return true;
            }

            string extension = Path.GetExtension(name);
            return !string.IsNullOrWhiteSpace(extension) && sourceTextExtensions.Contains(extension);
        }

        private static bool IsDockerfileName(string fileName)
        {
            return fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals(".dockerignore", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseSourceTextContentType(string normalizedContentType)
        {
            if (string.IsNullOrWhiteSpace(normalizedContentType)
                || string.Equals(normalizedContentType, DefaultContentType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (PreviewGeneratorProvider.GetGeneratorByContentType(normalizedContentType) is not null)
            {
                return false;
            }

            return normalizedContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || normalizedContentType.StartsWith("application/x-", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSourceTextFileNameRegexPattern()
        {
            var extensions = sourceTextExtensions
                .Select(extension => Regex.Escape(extension.TrimStart('.')))
                .OrderByDescending(extension => extension.Length)
                .ThenBy(extension => extension, StringComparer.Ordinal);

            return $"^(?:dockerfile(?:\\..*)?|\\.dockerignore|.+\\.(?:{string.Join("|", extensions)}))$";
        }

        private static bool ShouldForceExtensionContentType(string extension)
        {
            return extension is ".stl" or ".obj" or ".3mf";
        }

        private static string NormalizeContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return string.Empty;
            }

            string normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
            return normalized switch
            {
                "video/mov" => "video/quicktime",
                "video/x-quicktime" => "video/quicktime",
                "video/vnd.avi" => "video/x-msvideo",
                "video/avi" => "video/x-msvideo",
                "video/msvideo" => "video/x-msvideo",
                "image/x-heic" => "image/heic",
                "image/x-heif" => "image/heif",
                "audio/x-flac" => "audio/flac",
                "audio/x-wav" => "audio/wav",
                "application/vnd.ms-pki.stl" => "model/stl",
                _ => normalized,
            };
        }

        /// <summary>
        /// Gets chunks async.
        /// </summary>
        public async Task<List<Chunk>> GetChunksAsync(string[] chunkHashes, Guid userId, CancellationToken cancellationToken = default)
        {
            List<byte[]> normalizedHashes = [.. chunkHashes.Select(Hasher.FromHexStringHash)];
            List<Chunk> ownedChunks = await _dbContext.Chunks
                .Where(c => normalizedHashes.Contains(c.Hash))
                .Where(c => _dbContext.ChunkOwnerships.Any(co => co.ChunkHash == c.Hash && co.OwnerId == userId))
                .ToListAsync(cancellationToken);

            var chunkMap = ownedChunks.ToDictionary(c => Hasher.ToHexStringHash(c.Hash), StringComparer.OrdinalIgnoreCase);
            List<Chunk> result = [];
            foreach (var hash in chunkHashes)
            {
                if (!chunkMap.TryGetValue(hash, out var chunk))
                {
                    throw new EntityNotFoundException(nameof(Chunk));
                }
                result.Add(chunk);
            }
            return result;
        }

        /// <summary>
        /// Creates new file manifest async.
        /// </summary>
        public async Task<FileManifest> CreateNewFileManifestAsync(
            List<Chunk> chunks,
            string fileName,
            string? contentType,
            byte[] proposedContentHash,
            CancellationToken cancellationToken = default)
        {
            var newFileManifest = new FileManifest()
            {
                ContentType = ResolveContentType(fileName, contentType),
                SizeBytes = chunks.Sum(x => x.PlainSizeBytes),
                ProposedContentHash = proposedContentHash,
                PreviewGeneratorVersion = PreviewGeneratorProvider.DefaultGeneratorVersion,
            };

            await _dbContext.FileManifests.AddAsync(newFileManifest, cancellationToken);
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].GCScheduledAfter.HasValue)
                {
                    chunks[i].GCScheduledAfter = null;
                }

                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    ChunkHash = chunks[i].Hash,
                    FileManifest = newFileManifest,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk, cancellationToken);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return newFileManifest;
        }

        /// <summary>
        /// Clears gc schedules for manifest references.
        /// </summary>
        public async Task<int> ClearGcSchedulesForManifestReferencesAsync(
            Guid fileManifestId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Chunks
                .Where(c => c.GCScheduledAfter != null
                    && (_dbContext.FileManifestChunks.Any(fmc => fmc.FileManifestId == fileManifestId && fmc.ChunkHash == c.Hash)
                        || _dbContext.FileManifests.Any(fm => fm.Id == fileManifestId
                            && (fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash))))
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), cancellationToken);
        }
    }
}
