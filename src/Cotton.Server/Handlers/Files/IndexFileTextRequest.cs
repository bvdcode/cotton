// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
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
        ComputationService computation, ILogger<IndexFileTextRequestHandler> logger)
        : IRequestHandler<IndexFileTextRequest>
    {
        public async Task Handle(IndexFileTextRequest request, CancellationToken cancellationToken)
        {
            List<(FileManifest Manifest, string? Error)> files = [];
            float[][][] vectors = await computation.GetTextEmbeddingFragmentsAsync(
                ReadDocumentsAsync(cancellationToken), cancellationToken);
            if (files.Count == 0)
            {
                return;
            }

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
                try
                {
                    PipelineContext context = new()
                    {
                        FileSizeBytes = manifest.SizeBytes,
                        ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
                    };
                    await using Stream source = storage.GetBlobStream(manifest.FileManifestChunks.GetChunkHashes(), context);
                    text = await extractor.ExtractAsync(source, cancellationToken);
                    error = null;
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
