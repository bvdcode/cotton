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
                .SingleOrDefaultAsync(file => file.Id == request.FileManifestId, cancellationToken);
            if (manifest is null)
            {
                return;
            }
            IFileTextExtractor extractor = extractors.GetExtractor(manifest.ContentType)
                ?? extractors.GetExtractor(PdfTextExtractor.ContentType)
                ?? throw new InvalidOperationException("PDF text extraction is not registered.");
            float[][] vectors = [];
            string? error = null;
            try
            {
                PipelineContext context = new()
                {
                    FileSizeBytes = manifest.SizeBytes,
                    ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
                };
                await using Stream source = storage.GetBlobStream(manifest.FileManifestChunks.GetChunkHashes(), context);
                string text = await extractor.ExtractAsync(source, cancellationToken);
                vectors = await computation.GetTextEmbeddingFragmentsAsync(text, cancellationToken);
            }
            catch (FileTextExtractionException ex)
            {
                logger.LogWarning(ex, "Unable to index text for file manifest {FileManifestId}.", manifest.Id);
                error = ex.Message;
            }

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
