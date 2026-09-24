// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Computation;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Mappings;
using Cotton.Server.Models;
using Cotton.Server.Models.Computation;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class VectorLayoutSearchTests : IntegrationTestBase
    {
        private ServiceProvider _services = null!;
        private TeiTestHandler _worker = null!;
        private Node _folder = null!;

        public VectorLayoutSearchTests() : base($"cotton_vector_search_tests_{Guid.NewGuid():N}")
        {
        }

        [SetUp]
        public async Task SetUp()
        {
            await DbContext.Database.EnsureCreatedAsync();
            MapsterConfig.Register();
            _worker = new();
            ServerSettingsCache settings = new();
            settings.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                ComputionMode = ComputionMode.Remote,
                RemoteComputationRunnerUrl = "https://runner.example/",
            }));
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMediator();
            services.AddSingleton(DbContext);
            services.AddSingleton(settings);
            services.AddScoped<SettingsProvider>();
            services.AddComputationServices();
            services.AddHttpClient(TeiClient.RemoteClientName).ConfigurePrimaryHttpMessageHandler(() => _worker);
            services.AddLayoutSearchProviders();
            services.AddTransient<IRequestHandler<GetComputationServiceInfoQuery, ComputationServiceInfo>, GetComputationServiceInfoQueryHandler>();
            services.AddTransient<IRequestHandler<GetTextEmbeddingsRequest, float[][]>, GetTextEmbeddingsRequestHandler>();
            services.AddTransient<IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>, GetVectorIndexMetadataQueryHandler>();
            services.AddTransient<IRequestHandler<SearchVectorLayoutHitsQuery, IQueryable<LayoutSearchHit>?>, SearchVectorLayoutHitsQueryHandler>();
            services.AddTransient<IRequestHandler<SearchLayoutsQuery, PagedResult<SearchResultDto>>, SearchLayoutsQueryHandler>();
            _services = services.BuildServiceProvider();
            User user = new() { Username = "search", PasswordPhc = "phc", WebDavTokenPhc = "token" };
            _folder = CreateFolder(user, new Layout { Owner = user, IsActive = true });
            await DbContext.SaveChangesAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            DbContext.ChangeTracker.Clear();
            await DbContext.Database.EnsureDeletedAsync();
            await _services.DisposeAsync();
        }

        private Node CreateFolder(User owner, Layout layout, NodeType type = NodeType.Default)
        {
            Node node = new() { Owner = owner, Layout = layout, Type = type };
            node.SetName("files");
            DbContext.Nodes.Add(node);
            return node;
        }

        private NodeFile AddFile(string name, float[][] vectors, Node? folder = null, int version = VectorIndexDefinition.Version)
        {
            FileManifest manifest = new()
            {
                ContentType = "application/pdf",
                ProposedContentHash = Hasher.HashData(Encoding.UTF8.GetBytes(name)), SizeBytes = 100,
            };
            NodeFile file = new() { FileManifest = manifest, Node = folder ?? _folder, Owner = (folder ?? _folder).Owner };
            file.SetName(name);
            DbContext.NodeFiles.Add(file);
            for (int i = 0; i < vectors.Length; i++)
            {
                DbContext.FileEmbeddings.Add(new FileEmbedding
                {
                    FileManifest = manifest, FragmentIndex = i, IndexVersion = version, Embedding = vectors[i],
                });
            }
            return file;
        }

        private static float[] Direction(float x, float y)
        {
            float[] vector = new float[VectorIndexDefinition.Dimensions];
            vector[0] = x;
            vector[1] = y;
            return vector;
        }

        private async Task PrepareIndex()
        {
            if (!await DbContext.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await DbContext.SaveChangesAsync();
            await DbContext.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
            await DbContext.Database.CreateVectorCosineHnswIndexConcurrentlyAsync(VectorIndexDefinition.Expected);
        }

        private Task<PagedResult<SearchResultDto>> Search(bool deep = true, int page = 1, int pageSize = 20, string query = "report")
        {
            return _services.GetRequiredService<IMediator>().Send(
                new SearchLayoutsQuery(_folder.OwnerId, _folder.LayoutId, query, page, pageSize, deep));
        }
    }
}
