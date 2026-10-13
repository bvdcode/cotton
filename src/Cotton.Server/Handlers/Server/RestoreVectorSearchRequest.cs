// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public record RestoreVectorSearchRequest(PostgresRestoreResult Restore) : IRequest<VectorSearchRestoreStatus>;

    public class RestoreVectorSearchRequestHandler(
        CottonDbContext dbContext,
        IMediator mediator,
        ILogger<RestoreVectorSearchRequestHandler> logger)
        : IRequestHandler<RestoreVectorSearchRequest, VectorSearchRestoreStatus>
    {
        public async Task<VectorSearchRestoreStatus> Handle(RestoreVectorSearchRequest request, CancellationToken cancellationToken)
        {
            if (!request.Restore.HadVectorExtension && !request.Restore.HadVectorIndex)
            {
                return VectorSearchRestoreStatus.NotEnabled;
            }
            if (!await dbContext.Database.IsExtensionAvailableAsync(VectorIndexDefinition.ExtensionName, cancellationToken))
            {
                logger.LogWarning("Database restored without vector search: the pgvector package is unavailable on this PostgreSQL server.");
                return VectorSearchRestoreStatus.PackageMissing;
            }
            await mediator.Send(new EnsureVectorExtensionRequest { PrepareIndex = request.Restore.HadVectorIndex }, cancellationToken);
            return VectorSearchRestoreStatus.Enabled;
        }
    }
}
