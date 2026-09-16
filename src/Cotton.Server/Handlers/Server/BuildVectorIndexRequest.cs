// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Server
{
    public class BuildVectorIndexRequest : IRequest<string?>
    {
    }

    public class BuildVectorIndexRequestHandler(CottonDbContext dbContext, IMediator mediator)
        : IRequestHandler<BuildVectorIndexRequest, string?>
    {
        public async Task<string?> Handle(BuildVectorIndexRequest request, CancellationToken cancellationToken)
        {
            PostgresIndexStatus index = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
            if (index.IsBuilding || VectorIndexDefinition.IsReady(index))
            {
                return null;
            }
            if (index.Exists && !VectorIndexDefinition.IsCompatible(index))
            {
                return "pgvector_index_incompatible";
            }

            int? previousTimeout = dbContext.Database.GetCommandTimeout();
            try
            {
                if (index.Exists)
                {
                    dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(10));
                    DropIndexOperation operation = new()
                    {
                        Name = VectorIndexDefinition.Name,
                        Schema = VectorIndexDefinition.Schema
                    };
                    IReadOnlyList<MigrationCommand> commands = dbContext.Database.GetService<IMigrationsSqlGenerator>()
                        .Generate([operation]);
                    IRelationalConnection connection = dbContext.Database.GetService<IRelationalConnection>();
                    await dbContext.Database.GetService<IMigrationCommandExecutor>()
                        .ExecuteNonQueryAsync(commands, connection, cancellationToken);
                }

                dbContext.Database.SetCommandTimeout(0);
                await dbContext.Database.CreateVectorCosineHnswIndexConcurrentlyAsync(
                    VectorIndexDefinition.Schema,
                    VectorIndexDefinition.Table,
                    VectorIndexDefinition.Name,
                    VectorIndexDefinition.VectorColumn,
                    VectorIndexDefinition.Dimensions,
                    VectorIndexDefinition.VersionColumn,
                    VectorIndexDefinition.Version,
                    cancellationToken);
                PostgresIndexStatus result = await mediator.Send(new GetVectorIndexMetadataQuery(), cancellationToken);
                return VectorIndexDefinition.IsReady(result) || result.IsBuilding ? null : "pgvector_index_build_failed";
            }
            finally
            {
                dbContext.Database.SetCommandTimeout(previousTimeout);
            }
        }
    }
}
