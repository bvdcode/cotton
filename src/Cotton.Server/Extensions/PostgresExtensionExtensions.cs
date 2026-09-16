// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Cotton.Server.Extensions
{
    public static class PostgresExtensionExtensions
    {
        public static async Task EnsurePostgresExtensionAsync(
            this DatabaseFacade database,
            string extensionName,
            CancellationToken cancellationToken)
        {
            AlterDatabaseOperation operation = new();
            PostgresExtension.GetOrAddPostgresExtension(operation, extensionName, version: null);

            IReadOnlyList<MigrationCommand> commands = database.GetService<IMigrationsSqlGenerator>()
                .Generate([operation]);
            IRelationalConnection connection = database.GetService<IRelationalConnection>();

            await database.GetService<IMigrationCommandExecutor>()
                .ExecuteNonQueryAsync(commands, connection, cancellationToken);
        }
    }
}
