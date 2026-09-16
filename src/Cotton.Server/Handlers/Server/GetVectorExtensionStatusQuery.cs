// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.Handlers.Server
{
    public class GetVectorExtensionStatusQuery : IRequest<VectorExtensionStatusDto>
    {
    }

    public class GetVectorExtensionStatusQueryHandler(CottonDbContext dbContext)
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

                return new VectorExtensionStatusDto
                {
                    ExtensionEnabled = extensionEnabled,
                    ExtensionAvailable = extensionAvailable,
                    PostgresMajorVersion = connection.PostgreSqlVersion.Major,
                    DatabaseName = connection.Database,
                    VectorCount = vectorCount
                };
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
