// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Computation;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.Computation;
using Cotton.Storage.Abstractions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        private ServiceProvider _services = null!;
        private CottonDbContext _db = null!;
        private TeiTestHandler _worker = null!;
        private TextIndexPersistenceFailureInterceptor _failure = null!;
        private InMemoryStorage _storage = null!;
        private Node _folder = null!;
        private ServerSettingsCache _settingsCache = null!;

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

        private async Task<FileManifest> AddFileAsync(string name, string contentType, byte[] bytes, Node? node = null)
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

        private NodeFile AddReference(FileManifest manifest, string name, Node node)
        {
            NodeFile file = new() { FileManifest = manifest, Node = node, Owner = node.Owner };
            file.SetName(name);
            _db.NodeFiles.Add(file);
            return file;
        }

        private Task IndexAsync(Guid id) => _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(id), CancellationToken.None);
    }
}
