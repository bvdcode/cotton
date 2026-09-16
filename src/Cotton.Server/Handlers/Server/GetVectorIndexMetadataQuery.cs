// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class GetVectorIndexMetadataQuery : IRequest<PostgresIndexStatus>
    {
    }

    public class GetVectorIndexMetadataQueryHandler(CottonDbContext dbContext)
        : IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>
    {
        public Task<PostgresIndexStatus> Handle(GetVectorIndexMetadataQuery request, CancellationToken cancellationToken)
        {
            return dbContext.Database.GetIndexStatusAsync(
                VectorIndexDefinition.Schema,
                VectorIndexDefinition.Table,
                VectorIndexDefinition.Name,
                cancellationToken);
        }
    }
}
