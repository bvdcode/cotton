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

namespace Cotton.Server.Handlers.Files
{
    public record IndexFileTextRequest(Guid FileManifestId) : IRequest;

    public class IndexFileTextRequestHandler(
        CottonDbContext dbContext, IStoragePipeline storage, FileTextExtractorProvider extractors,
        ComputationService computation, ILogger<IndexFileTextRequestHandler> logger)
        : IRequestHandler<IndexFileTextRequest>
    {
        public async Task Handle(IndexFileTextRequest request, CancellationToken cancellationToken)
        {
            FileManifest? manifest = await FileTextIndexQuery.Pending(dbContext, extractors.GetSupportedContentTypes())
                .Include(file => file.FileManifestChunks).ThenInclude(chunk => chunk.Chunk)
                .Include(file => file.NodeFiles)
                .AsSplitQuery()
                .SingleOrDefaultAsync(file => file.Id == request.FileManifestId, cancellationToken);
            if (manifest is null)
            {
                return;
            }
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
            if (text is null && error is null)
            {
                return;
            }
            float[][] vectors = text is not null
                ? await computation.GetTextEmbeddingFragmentsAsync(text, cancellationToken)
                : [];

            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.FileEmbeddings.Where(embedding => embedding.FileManifestId == manifest.Id)
                .ExecuteDeleteAsync(cancellationToken);
            for (int index = 0; index < vectors.Length; index++)
            {
                dbContext.FileEmbeddings.Add(new FileEmbedding
                {
                    FileManifestId = manifest.Id,
                    IndexVersion = VectorIndexDefinition.Version,
                    FragmentIndex = index,
                    Embedding = vectors[index],
                });
            }
            manifest.TextIndexVersion = VectorIndexDefinition.Version;
            manifest.TextIndexError = error;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
