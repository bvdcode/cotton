// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class PreviewTestBase : IntegrationTestBase
    {
        protected PreviewTestPipeline Pipeline { get; private set; } = null!;
        private protected const string PreviewRouteBase = "/api/v1/preview";

        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;
        private protected MetadataPersistenceFailureInterceptor _metadataFailure = null!;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True);
                Assert.That(creator.HasTables(), Is.False);
            });

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres"
            };

            Dictionary<string, string?> overrides = new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = csb.Host,
                ["DatabaseSettings:Port"] = csb.Port.ToString(),
                ["DatabaseSettings:Database"] = csb.Database,
                ["DatabaseSettings:Username"] = csb.Username,
                ["DatabaseSettings:Password"] = csb.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
            };

            _metadataFailure = new MetadataPersistenceFailureInterceptor();
            _factory = new TestAppFactory(overrides, services =>
            {
                services.AddSingleton(_metadataFailure);
                services.AddDbContext<CottonDbContext>((serviceProvider, options) =>
                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<MetadataPersistenceFailureInterceptor>()));
            });
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            Pipeline = new PreviewTestPipeline(_factory, _client, DbContext);
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();

            _client = null;
            _factory = null;
        }
    }
}
