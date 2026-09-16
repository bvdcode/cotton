// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Server
{
    public class GetVectorExtensionStatusQuery : IRequest<VectorExtensionStatusDto>
    {
    }

    public class GetVectorExtensionStatusQueryHandler(
        CottonDbContext dbContext, IMediator mediator, VectorIndexBuildState indexBuildState)
        : IRequestHandler<GetVectorExtensionStatusQuery, VectorExtensionStatusDto>
    {
        public async Task<VectorExtensionStatusDto> Handle(
            GetVectorExtensionStatusQuery request,
            CancellationToken cancellationToken)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                NpgsqlConnection connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
                bool extensionEnabled = await dbContext.Database.IsExtensionInstalledAsync("vector", cancellationToken);
                bool extensionAvailable = await dbContext.Database.IsExtensionAvailableAsync("vector", cancellationToken);
                long vectorCount = await dbContext.FileEmbeddings.LongCountAsync(cancellationToken);
                PostgresIndexStatus index = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
                (bool running, string? errorCode) = indexBuildState.GetSnapshot();
                if (index.Exists && !VectorIndexDefinition.IsCompatible(index))
                {
                    errorCode = "pgvector_index_incompatible";
                }
                else if (index.Exists && !index.IsValid && !index.IsBuilding && !running && errorCode is null)
                {
                    errorCode = "pgvector_index_build_failed";
                }

                return new VectorExtensionStatusDto
                {
                    ExtensionEnabled = extensionEnabled,
                    ExtensionAvailable = extensionAvailable,
                    PostgresMajorVersion = connection.PostgreSqlVersion.Major,
                    DatabaseName = connection.Database,
                    VectorCount = vectorCount,
                    IndexReady = VectorIndexDefinition.IsReady(index),
                    IndexBuilding = running || index.IsBuilding,
                    IndexSizeBytes = index.SizeBytes,
                    IndexErrorCode = VectorIndexDefinition.IsReady(index) ? null : errorCode,
                    IndexCreateSql = VectorIndexDefinition.ManualCreateSql
                };
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
