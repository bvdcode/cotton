// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Logging;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseLoggingTests
    {
        [TestCase("chunks", false)]
        [TestCase("chunks", true)]
        [TestCase("chunk_ownerships", false)]
        [TestCase("chunk_ownerships", true)]
        public async Task Expected_Conflict_Is_Quiet_Only_Within_Recovery_Scope(string table, bool saving)
        {
            using NUnitLoggerProvider logs = new();
            using ILoggerFactory inner = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using DatabaseLoggerFactory factory = new(inner);
            ILogger logger = factory.CreateLogger(saving ? DbLoggerCategory.Update.Name : DbLoggerCategory.Database.Command.Name);
            Exception error = Conflict(table, PostgresErrorCodes.UniqueViolation, saving);
            EventId eventId = saving ? CoreEventId.SaveChangesFailed : RelationalEventId.CommandError;

            logger.LogError(eventId, error, "Outside recovery");
            using (ChunkUpsertConflictScope scope = new())
            {
                await Task.Yield();
                logger.LogError(eventId, error, "Handled conflict");
            }
            logger.LogError(eventId, error, "After recovery");

            Assert.That(logs.Messages, Has.Count.EqualTo(2));
            Assert.That(logs.Messages.Any(message => message.Contains("Handled conflict")), Is.False);
        }

        [TestCase("users", PostgresErrorCodes.UniqueViolation)]
        [TestCase("chunks", PostgresErrorCodes.ForeignKeyViolation)]
        [TestCase("chunk_ownerships", PostgresErrorCodes.CheckViolation)]
        public void Other_Database_Errors_Remain_Visible(string table, string code)
        {
            using NUnitLoggerProvider logs = new();
            using ILoggerFactory inner = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using DatabaseLoggerFactory factory = new(inner);
            ILogger logger = factory.CreateLogger(DbLoggerCategory.Update.Name);
            Exception error = Conflict(table, code, saving: true);

            using (ChunkUpsertConflictScope scope = new())
            {
                logger.LogError(CoreEventId.SaveChangesFailed, error, "Unexpected error");
                logger.LogWarning("Database warning");
            }

            Assert.That(logs.Messages, Has.Count.EqualTo(2));
            Assert.That(logs.Messages.Any(message => message.Contains(code)), Is.True);
        }

        [Test]
        public async Task Recovery_Scope_Does_Not_Hide_Errors_In_Another_Request()
        {
            using NUnitLoggerProvider logs = new();
            using ILoggerFactory inner = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using DatabaseLoggerFactory factory = new(inner);
            ILogger logger = factory.CreateLogger(DbLoggerCategory.Database.Command.Name);
            Exception error = Conflict("chunks", PostgresErrorCodes.UniqueViolation, saving: false);
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task recovering = Task.Run(async () =>
            {
                using ChunkUpsertConflictScope scope = new();
                entered.SetResult();
                await release.Task;
                logger.LogError(RelationalEventId.CommandError, error, "Handled in first request");
            });

            await entered.Task;
            try
            {
                logger.LogError(RelationalEventId.CommandError, error, "Unhandled in second request");
            }
            finally
            {
                release.SetResult();
                await recovering;
            }

            Assert.That(logs.Messages, Has.Count.EqualTo(1));
            Assert.That(logs.Messages.Single(), Does.Contain("Unhandled in second request"));
        }

        [Test]
        public void Nested_Recovery_Restores_The_Previous_Scope()
        {
            using (ChunkUpsertConflictScope outer = new())
            {
                using (ChunkUpsertConflictScope inner = new())
                {
                    Assert.That(ChunkUpsertConflictScope.IsActive, Is.True);
                }
                Assert.That(ChunkUpsertConflictScope.IsActive, Is.True);
            }
            Assert.That(ChunkUpsertConflictScope.IsActive, Is.False);
        }

        [Test]
        public void Application_Errors_Are_Not_Filtered_Or_Disposed_With_The_Database_Factory()
        {
            using NUnitLoggerProvider logs = new();
            using ILoggerFactory inner = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using (DatabaseLoggerFactory factory = new(inner))
            {
                ILogger logger = factory.CreateLogger(typeof(ChunkIngestService).FullName!);
                using ChunkUpsertConflictScope scope = new();
                logger.LogError(RelationalEventId.CommandError,
                    Conflict("chunks", PostgresErrorCodes.UniqueViolation, saving: false), "Unhandled application error");
            }

            inner.CreateLogger("Application").LogWarning("Logging remains available");
            Assert.That(logs.Messages, Has.Count.EqualTo(2));
        }

        private static Exception Conflict(string table, string code, bool saving)
        {
            PostgresException error = new("Database constraint conflict", "ERROR", "ERROR", code, tableName: table);
            return saving ? new DbUpdateException("Save failed", error) : error;
        }
    }
}
