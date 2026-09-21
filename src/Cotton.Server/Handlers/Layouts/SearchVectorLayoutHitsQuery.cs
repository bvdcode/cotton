// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Computation;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Services.Search;
using Cotton.Topology;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Net;

namespace Cotton.Server.Handlers.Layouts
{
    public record SearchVectorLayoutHitsQuery(LayoutSearchRequest Search) : IRequest<IQueryable<LayoutSearchHit>?>;

    public class SearchVectorLayoutHitsQueryHandler(CottonDbContext dbContext, IMediator mediator)
        : IRequestHandler<SearchVectorLayoutHitsQuery, IQueryable<LayoutSearchHit>?>
    {
        public const int MaximumNearestFragments = 1000;

        public async Task<IQueryable<LayoutSearchHit>?> Handle(
            SearchVectorLayoutHitsQuery request, CancellationToken cancellationToken)
        {
            LayoutSearchRequest search = request.Search;
            IQueryable<NodeFile> files = dbContext.NodeFiles.AsNoTracking().AccessibleTo(search.UserId).Where(file =>
                file.Node.LayoutId == search.LayoutId
                && file.Node.Type == NodeType.Default
                && (file.OriginalNodeFileId == Guid.Empty || file.OriginalNodeFileId == file.Id));

            IQueryable<FileEmbedding> embeddings = dbContext.FileEmbeddings.AsNoTracking().Where(embedding =>
                embedding.IndexVersion == VectorIndexDefinition.Version
                && files.Any(file => file.FileManifestId == embedding.FileManifestId));

            if (!await embeddings.AnyAsync(cancellationToken))
            {
                return null;
            }

            PostgresIndexStatus index = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
            if (!VectorIndexDefinition.IsReady(index))
            {
                throw new WebApiException(HttpStatusCode.ServiceUnavailable, string.Empty, "Smart search index is not ready.");
            }

            float[][] vectors = await mediator.Send(new GetTextEmbeddingsRequest([search.Query.Trim()]), cancellationToken);
            float[] queryVector = vectors[0];

            var nearest = embeddings
                .OrderBy(embedding => CottonDbContext.ToVector(
                    embedding.Embedding, VectorIndexDefinition.Dimensions, true).CosineDistance(
                        CottonDbContext.ToVector(queryVector, VectorIndexDefinition.Dimensions, true)))
                .Take(MaximumNearestFragments)
                .Select(embedding => new
                {
                    embedding.FileManifestId,
                    Distance = CottonDbContext.ToVector(
                        embedding.Embedding, VectorIndexDefinition.Dimensions, true).CosineDistance(
                            CottonDbContext.ToVector(queryVector, VectorIndexDefinition.Dimensions, true)),
                });

            return from embedding in nearest
                   join file in files on embedding.FileManifestId equals file.FileManifestId
                   select new LayoutSearchHit
                   {
                       Kind = LayoutSearchHitKind.File,
                       Id = file.Id,
                       NodeIdForPath = file.NodeId,
                       Name = file.Name,
                       NameKey = file.NameKey,
                       Score = 1 - embedding.Distance,
                   };
        }
    }
}
