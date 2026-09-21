// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.Computation;
using Cotton.Server.Services.Search;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.TextExtraction;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cotton.Server.Handlers.Files
{
    public record IndexFileTextRequest(IEnumerable<Guid> FileManifestIds) : IRequest
    {
        public IndexFileTextRequest(Guid fileManifestId) : this([fileManifestId])
        {
        }
    }

    public class IndexFileTextRequestHandler(
        CottonDbContext dbContext, IStoragePipeline storage, FileTextExtractorProvider extractors,
        ComputationService computation, IOptions<TextIndexingOptions> options, ILogger<IndexFileTextRequestHandler> logger)
        : IRequestHandler<IndexFileTextRequest>
    {
        public async Task Handle(IndexFileTextRequest request, CancellationToken cancellationToken)
        {
            long startedAt = Stopwatch.GetTimestamp();
            List<(FileManifest Manifest, string? Error)> files = [];
            float[][][] vectors = await computation.GetTextEmbeddingFragmentsAsync(
                ReadDocumentsAsync(cancellationToken), cancellationToken);
            if (files.Count == 0)
            {
                return;
            }

            int vectorCount = vectors.Sum(document => document.Length);
            logger.LogInformation("Saving text index batch: {FileCount} file manifests, {VectorCount} vectors.",
                files.Count, vectorCount);
            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
            {
                (FileManifest manifest, string? error) = files[fileIndex];
                await dbContext.FileEmbeddings.Where(embedding => embedding.FileManifestId == manifest.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                for (int fragment = 0; fragment < vectors[fileIndex].Length; fragment++)
                {
                    dbContext.FileEmbeddings.Add(new FileEmbedding
                    {
                        FileManifestId = manifest.Id,
                        IndexVersion = VectorIndexDefinition.Version,
                        FragmentIndex = fragment,
                        Embedding = vectors[fileIndex][fragment],
                    });
                }
                manifest.TextIndexVersion = VectorIndexDefinition.Version;
                manifest.TextIndexError = error;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Text index batch saved: {FileCount} file manifests, {VectorCount} vectors in {ElapsedSeconds:F1} seconds.",
                files.Count, vectorCount, Stopwatch.GetElapsedTime(startedAt).TotalSeconds);

            async IAsyncEnumerable<string> ReadDocumentsAsync([EnumeratorCancellation] CancellationToken token)
            {
                string[] contentTypes = extractors.GetSupportedContentTypes();
                foreach (Guid id in request.FileManifestIds.Distinct())
                {
                    token.ThrowIfCancellationRequested();
                    FileManifest? manifest = await FileTextIndexQuery.Pending(dbContext, contentTypes)
                        .Include(file => file.FileManifestChunks).ThenInclude(chunk => chunk.Chunk)
                        .Include(file => file.NodeFiles)
                        .AsSplitQuery()
                        .SingleOrDefaultAsync(file => file.Id == id, token);
                    if (manifest is null)
                    {
                        continue;
                    }
                    logger.LogInformation("Indexing text for file manifest {FileManifestId}, size {SizeBytes} bytes.",
                        manifest.Id, manifest.SizeBytes);
                    (string? text, string? error) = await ExtractTextAsync(manifest, token);
                    if (text is null && error is null)
                    {
                        continue;
                    }
                    files.Add((manifest, error));
                    yield return text ?? string.Empty;
                }
            }
        }

        private async Task<(string? Text, string? Error)> ExtractTextAsync(
            FileManifest manifest, CancellationToken cancellationToken)
        {
            IEnumerable<string> contentTypes = manifest.NodeFiles
                .Select(file => file.ContentType)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            string? text = null;
            string? error = null;
            foreach (string contentType in contentTypes)
            {
                IFileTextExtractor? extractor = extractors.GetExtractor(contentType);
                if (extractor is null)
                {
                    continue;
                }
                long fileSizeLimit = options.Value.MaxStructuredFileBytes;
                if (manifest.SizeBytes > fileSizeLimit
                    && TextIndexingOptions.StructuredContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                {
                    error = $"file_too_large: {contentType} exceeds {fileSizeLimit} bytes.";
                    logger.LogInformation(
                        "Skipping text indexing for file manifest {FileManifestId}: {ContentType}, {SizeBytes} bytes exceeds the {MaxFileBytes} byte limit.",
                        manifest.Id, contentType, manifest.SizeBytes, fileSizeLimit);
                    continue;
                }
                try
                {
                    PipelineContext context = new()
                    {
                        FileSizeBytes = manifest.SizeBytes,
                        ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
                    };
                    await using Stream source = storage.GetBlobStream(manifest.FileManifestChunks.GetChunkHashes(), context);
                    int limit = options.Value.MaxExtractedTextBytes;
                    TextExtractionResult result = await extractor.ExtractAsync(source, limit, cancellationToken);
                    text = result.Text;
                    error = result.IsTruncated
                        ? $"text_truncated: indexed the beginning within {limit} UTF-8 bytes."
                        : null;
                    if (result.IsTruncated)
                    {
                        logger.LogInformation("Text indexing for file manifest {FileManifestId} was truncated at {MaxExtractedTextBytes} UTF-8 bytes.",
                            manifest.Id, limit);
                    }
                    break;
                }
                catch (FileTextExtractionException ex)
                {
                    logger.LogWarning(ex,
                        "Unable to extract text for file manifest {FileManifestId} content type {ContentType}.",
                        manifest.Id, contentType);
                    error = ex.Message;
                }
            }
            return (text, error);
        }
    }
}
