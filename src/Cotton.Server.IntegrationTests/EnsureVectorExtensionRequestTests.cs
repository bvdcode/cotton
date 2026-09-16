// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Helpers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class EnsureVectorExtensionRequestTests
    {
        [Test]
        public async Task Handle_GeneratesOnlyIdempotentVectorExtensionCommand()
        {
            await using CottonDbContext context = CreateContext();
            RecordingMigrationCommandExecutor executor = GetExecutor(context);
            EnsureVectorExtensionRequestHandler handler = CreateHandler(context);

            await handler.Handle(new EnsureVectorExtensionRequest(), CancellationToken.None);
            await handler.Handle(new EnsureVectorExtensionRequest(), CancellationToken.None);

            Assert.That(executor.Commands, Has.Count.EqualTo(2));
            Assert.That(executor.Commands.All(command =>
                command.Trim() == "CREATE EXTENSION IF NOT EXISTS vector;"), Is.True);
            Assert.That(context.ChangeTracker.Entries(), Is.Empty);
        }

        [TestCase(PostgresErrorCodes.InsufficientPrivilege, "pgvector_permission_denied")]
        [TestCase(PostgresErrorCodes.FeatureNotSupported, "pgvector_package_missing")]
        [TestCase(PostgresErrorCodes.UndefinedFile, "pgvector_package_missing")]
        public void Handle_ReportsConfigurationFailures(string sqlState, string expectedCode)
        {
            using CottonDbContext context = CreateContext();
            GetExecutor(context).Failure = new PostgresException("test failure", "ERROR", "ERROR", sqlState);

            WebApiException? exception = Assert.ThrowsAsync<WebApiException>(() =>
                CreateHandler(context).Handle(new EnsureVectorExtensionRequest(), CancellationToken.None));

            Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(exception.GetErrorModel().Extensions["code"], Is.EqualTo(expectedCode));
            Assert.That(exception.ObjectName, Is.Empty);
        }

        [Test]
        public void Handle_DoesNotMaskUnexpectedDatabaseErrors()
        {
            using CottonDbContext context = CreateContext();
            PostgresException failure = new("test failure", "ERROR", "ERROR", PostgresErrorCodes.ConnectionFailure);
            GetExecutor(context).Failure = failure;

            PostgresException? exception = Assert.ThrowsAsync<PostgresException>(() =>
                CreateHandler(context).Handle(new EnsureVectorExtensionRequest(), CancellationToken.None));

            Assert.That(exception, Is.SameAs(failure));
        }

        [Test]
        public void Handle_RespectsCancellation()
        {
            using CottonDbContext context = CreateContext();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                CreateHandler(context).Handle(new EnsureVectorExtensionRequest(), cancellation.Token));
            Assert.That(GetExecutor(context).Commands, Is.Empty);
        }

        private static CottonDbContext CreateContext()
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .ReplaceService<IMigrationCommandExecutor, RecordingMigrationCommandExecutor>()
                .Options;
            return new CottonDbContext(options);
        }

        private static RecordingMigrationCommandExecutor GetExecutor(CottonDbContext context)
        {
            return (RecordingMigrationCommandExecutor)context.GetService<IMigrationCommandExecutor>();
        }

        private static EnsureVectorExtensionRequestHandler CreateHandler(CottonDbContext context)
        {
            return new EnsureVectorExtensionRequestHandler(
                context, NullLogger<EnsureVectorExtensionRequestHandler>.Instance);
        }
    }
}
