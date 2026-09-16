// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Npgsql;
using System.Net;

namespace Cotton.Server.Handlers.Server
{
    public class EnsureVectorExtensionRequest : IRequest
    {
    }

    public class EnsureVectorExtensionRequestHandler(
        CottonDbContext dbContext,
        ILogger<EnsureVectorExtensionRequestHandler> logger) : IRequestHandler<EnsureVectorExtensionRequest>
    {
        private const string ExtensionName = "vector";

        public async Task Handle(EnsureVectorExtensionRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await dbContext.Database.EnsurePostgresExtensionAsync(ExtensionName, cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                logger.LogWarning(ex, "The database role cannot enable the vector extension.");
                throw new WebApiException(
                    HttpStatusCode.Conflict,
                    string.Empty,
                    "Run CREATE EXTENSION IF NOT EXISTS vector; in this database as a PostgreSQL administrator.",
                    new Dictionary<string, object?> { ["code"] = "pgvector_permission_denied" });
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.FeatureNotSupported
                or PostgresErrorCodes.UndefinedFile)
            {
                logger.LogWarning(ex, "The vector extension is not available on the PostgreSQL server.");
                throw new WebApiException(
                    HttpStatusCode.Conflict,
                    string.Empty,
                    "Install the pgvector package for this PostgreSQL server version, then retry.",
                    new Dictionary<string, object?> { ["code"] = "pgvector_package_missing" });
            }

            logger.LogInformation("The vector extension is enabled in the current database.");
        }
    }
}
