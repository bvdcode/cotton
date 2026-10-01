// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using Microsoft.Extensions.Configuration;
using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using Cotton.Server.Services.Computation;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Computation;
using Cotton.Server.Handlers.Computation;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class FileTextIndexingTestBase()
        : IntegrationTestBase($"cotton_text_index_tests_{Guid.NewGuid():N}")
    {
        private protected ServiceProvider _services = null!;
        private protected CottonDbContext _db = null!;
        private protected TeiTestHandler _worker = null!;
        private protected TextIndexPersistenceFailureInterceptor _failure = null!;
        private protected InMemoryStorage _storage = null!;
        private protected Node _folder = null!;
        private protected ServerSettingsCache _settingsCache = null!;

        [SetUp]
        public async Task SetUp()
        {
            _failure = new();
            _db = new(new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(DbContext.Database.GetConnectionString()).AddInterceptors(_failure).Options);
            await _db.Database.EnsureCreatedAsync();
            _worker = new() { MaxInputTokens = 16, MaxBatchTokens = 32, MaxBatchInputs = 2 };
            _storage = new();
            _settingsCache = new();
            _settingsCache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                AllowGlobalIndexing = true,
                ComputionMode = ComputionMode.Remote,
                RemoteComputationRunnerUrl = "https://runner.example/",
            }));
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddMediator();
            services.AddSingleton(_db);
            services.AddSingleton(_settingsCache);
            services.AddScoped<SettingsProvider>();
            services.AddSingleton<IStoragePipeline>(_storage);
            services.AddComputationServices();
            services.AddHttpClient(TeiClient.RemoteClientName).ConfigurePrimaryHttpMessageHandler(() => _worker);
            services.AddSingleton<PerfTracker>();
            services.AddTransient<GenerateFileEmbeddingsJob>();
            services.AddTransient<IRequestHandler<IndexFileTextRequest>, IndexFileTextRequestHandler>();
            services.AddTransient<IRequestHandler<RecoverFileTextIndexRequest>, RecoverFileTextIndexRequestHandler>();
            services.AddTransient<IRequestHandler<GetComputationServiceInfoQuery, ComputationServiceInfo>, GetComputationServiceInfoQueryHandler>();
            services.AddTransient<IRequestHandler<GetComputationStatusQuery, ComputationStatus>, GetComputationStatusQueryHandler>();
            services.AddTransient<IRequestHandler<GetTextEmbeddingFragmentsRequest, float[][][]>, GetTextEmbeddingFragmentsRequestHandler>();
            services.AddTransient<IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>, GetVectorIndexMetadataQueryHandler>();
            services.AddTransient<IRequestHandler<BuildVectorIndexRequest, string?>, BuildVectorIndexRequestHandler>();
            _services = services.BuildServiceProvider();
            User user = new() { Username = "textindex", PasswordPhc = "phc", WebDavTokenPhc = "token" };
            _folder = new() { Owner = user, Layout = new Layout { Owner = user, IsActive = true }, Type = NodeType.Default };
            _folder.SetName("files");
            _db.Nodes.Add(_folder);
            await _db.SaveChangesAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            _db.ChangeTracker.Clear();
            await _db.Database.EnsureDeletedAsync();
            await _services.DisposeAsync();
        }

        private protected async Task<FileManifest> AddFileAsync(string name, string contentType, byte[] bytes, Node? node = null)
        {
            byte[] hash = Hasher.HashData(bytes);
            FileManifest manifest = new() { ContentType = contentType, ProposedContentHash = hash, SizeBytes = bytes.Length };
            Chunk chunk = new() { Hash = hash, PlainSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length };
            manifest.FileManifestChunks.Add(new FileManifestChunk { Chunk = chunk, ChunkHash = hash });
            AddReference(manifest, name, node ?? _folder);
            await _db.SaveChangesAsync();
            using MemoryStream source = new(bytes);
            await _storage.WriteAsync(Hasher.ToHexStringHash(hash), source);
            return manifest;
        }

        private protected NodeFile AddReference(FileManifest manifest, string name, Node node)
        {
            NodeFile file = new() { FileManifest = manifest, Node = node, Owner = node.Owner };
            file.SetName(name);
            _db.NodeFiles.Add(file);
            return file;
        }

        private protected Task IndexAsync(Guid id) => _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(id), CancellationToken.None);

        private protected async Task PrepareVectorIndexAsync()
        {
            if (!await _db.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await _db.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
            await _services.GetRequiredService<IMediator>().Send(new BuildVectorIndexRequest(), CancellationToken.None);
        }
    }
}
