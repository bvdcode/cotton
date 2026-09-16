// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class VectorIndexPreparationTests : IntegrationTestBase
    {
        private ServiceProvider _services = null!;

        public VectorIndexPreparationTests() : base($"cotton_vector_index_tests_{Guid.NewGuid():N}")
        {
        }

        [SetUp]
        public async Task SetUp()
        {
            await DbContext.Database.EnsureCreatedAsync();
            ServiceCollection services = new();
            services.AddSingleton(DbContext);
            services.AddMediator();
            services.AddTransient<IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>, GetVectorIndexMetadataQueryHandler>();
            services.AddTransient<IRequestHandler<BuildVectorIndexRequest, string?>, BuildVectorIndexRequestHandler>();
            _services = services.BuildServiceProvider();
        }

        [TearDown]
        public async Task TearDown()
        {
            DbContext.ChangeTracker.Clear();
            await DbContext.Database.EnsureDeletedAsync();
            await _services.DisposeAsync();
        }

        [Test]
        public async Task Status_WorksWithoutTheVectorExtension()
        {
            PostgresIndexStatus status = await Send(new GetVectorIndexMetadataQuery());
            Assert.That(status.Exists, Is.False);
            Assert.That(VectorIndexDefinition.IsReady(status), Is.False);
            Assert.That(status.IsBuilding, Is.False);
            Assert.That(status.SizeBytes, Is.Zero);
        }

        [Test]
        public async Task Build_CreatesAValidPartialIndexAndCanBeRepeated()
        {
            await EnableVectorAsync();
            FileManifest manifest = new()
            {
                ContentType = "text/plain", ProposedContentHash = [1, 2, 3], SizeBytes = 3
            };
            DbContext.FileEmbeddings.AddRange(
                new FileEmbedding
                {
                    FileManifest = manifest,
                    IndexVersion = VectorIndexDefinition.Version,
                    Embedding = Enumerable.Repeat(0.1f, VectorIndexDefinition.Dimensions).ToArray()
                },
                new FileEmbedding
                {
                    FileManifest = manifest,
                    IndexVersion = VectorIndexDefinition.Version + 1,
                    Embedding = [1, 2]
                });
            await DbContext.SaveChangesAsync();

            Assert.That(await Send(new BuildVectorIndexRequest()), Is.Null);
            PostgresIndexStatus first = await Send(new GetVectorIndexMetadataQuery());
            Assert.That(VectorIndexDefinition.IsReady(first), Is.True);
            Assert.That(first.IsBuilding, Is.False);
            Assert.That(first.SizeBytes, Is.GreaterThan(0));
            Assert.That(first.Definition, Is.EqualTo(VectorIndexDefinition.ExpectedDefinition));
            Assert.That(await Send(new BuildVectorIndexRequest()), Is.Null);
            PostgresIndexStatus second = await Send(new GetVectorIndexMetadataQuery());
            Assert.That(second.Definition, Is.EqualTo(first.Definition));
            Assert.That(second.SizeBytes, Is.EqualTo(first.SizeBytes));
            Assert.That(await DbContext.FileEmbeddings.CountAsync(), Is.EqualTo(2));
        }

        [Test]
        public async Task Build_RepairsAnInvalidIndexAfterBadDataIsCorrected()
        {
            await EnableVectorAsync();
            FileEmbedding embedding = new()
            {
                FileManifest = new FileManifest
                {
                    ContentType = "text/plain", ProposedContentHash = [4, 5, 6], SizeBytes = 3
                },
                IndexVersion = VectorIndexDefinition.Version,
                Embedding = [1, 2]
            };
            DbContext.FileEmbeddings.Add(embedding);
            await DbContext.SaveChangesAsync();
            Assert.ThrowsAsync<PostgresException>(() => Send(new BuildVectorIndexRequest()));
            PostgresIndexStatus failed = await Send(new GetVectorIndexMetadataQuery());
            Assert.That(failed.Exists, Is.True);
            Assert.That(VectorIndexDefinition.IsReady(failed), Is.False);

            embedding.Embedding = Enumerable.Repeat(0.2f, VectorIndexDefinition.Dimensions).ToArray();
            await DbContext.SaveChangesAsync();
            Assert.That(await Send(new BuildVectorIndexRequest()), Is.Null);
            Assert.That(VectorIndexDefinition.IsReady(await Send(new GetVectorIndexMetadataQuery())), Is.True);
        }

        private Task<TResponse> Send<TResponse>(IRequest<TResponse> request)
        {
            return _services.GetRequiredService<IMediator>().Send(request, CancellationToken.None);
        }

        private async Task EnableVectorAsync()
        {
            if (!await DbContext.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await DbContext.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
        }
    }
}
