// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RecordingMigrationCommandExecutor : IMigrationCommandExecutor
    {
        public List<string> Commands { get; } = [];

        public Exception? Failure { get; set; }

        public void ExecuteNonQuery(IEnumerable<MigrationCommand> migrationCommands, IRelationalConnection connection)
        {
            throw new NotSupportedException();
        }

        public int ExecuteNonQuery(
            IReadOnlyList<MigrationCommand> migrationCommands,
            IRelationalConnection connection,
            MigrationExecutionState executionState,
            bool commitTransaction,
            IsolationLevel? isolationLevel = null)
        {
            throw new NotSupportedException();
        }

        public Task ExecuteNonQueryAsync(
            IEnumerable<MigrationCommand> migrationCommands,
            IRelationalConnection connection,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.AddRange(migrationCommands.Select(command => command.CommandText));
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }
            return Task.CompletedTask;
        }

        public Task<int> ExecuteNonQueryAsync(
            IReadOnlyList<MigrationCommand> migrationCommands,
            IRelationalConnection connection,
            MigrationExecutionState executionState,
            bool commitTransaction,
            IsolationLevel? isolationLevel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
