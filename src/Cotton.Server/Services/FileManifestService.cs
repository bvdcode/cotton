// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class FileManifestService(CottonDbContext _dbContext)
    {
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

                [".stl"] = "model/stl",
                [".obj"] = "model/obj",
                [".3mf"] = "model/3mf",
            };

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
                return normalizedContentType;
            }

            if (!string.IsNullOrWhiteSpace(fileName)
                && fileExtensionContentTypeProvider.TryGetContentType(fileName, out string? detectedContentType)
                && !string.IsNullOrWhiteSpace(detectedContentType))
            {
                return NormalizeContentType(detectedContentType);
            }

            return string.IsNullOrWhiteSpace(normalizedContentType)
                ? DefaultContentType
                : normalizedContentType;
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
                "image/x-heic" => "image/heic",
                "image/x-heif" => "image/heif",
                "audio/x-flac" => "audio/flac",
                "audio/x-wav" => "audio/wav",
                _ => normalized,
            };
        }

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
    }
}
