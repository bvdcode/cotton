// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services.Bridge;
using Cotton.Server.Services.Computation;
using System.Net.Http.Headers;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class StartupLifecycleTestBase()
        : IntegrationTestBase("cotton_dev_tests_startup_" + Guid.NewGuid().ToString("N"))
    {
        private protected BridgeTestTransport _bridge = null!;

        private protected void ConfigureComputationClients(IServiceCollection services)
        {
            _bridge = new BridgeTestTransport();
            services.AddHttpClient(TeiClient.RemoteClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _runner);
            services.AddHttpClient(BridgeCredentialProvider.RegistrationClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _bridge);
            services.AddHttpClient(CottonBridgeServiceCollectionExtensions.ComputationClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _runner);
        }

        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;
        private protected TeiTestHandler _runner = null!;

        [SetUp]
        public void SetUp()
        {
            _client = null;
            _factory = null;

            NpgsqlConnection.ClearAllPools();
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();
            NpgsqlConnection.ClearAllPools();

            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True);
                Assert.That(creator.HasTables(), Is.False);
            });

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = CurrentDatabaseName,
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

            _runner = new TeiTestHandler();
            _factory = new TestAppFactory(overrides, ConfigureComputationClients);
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
            NpgsqlConnection.ClearAllPools();
            DbContext.GetService<IRelationalDatabaseCreator>().EnsureDeleted();
            NpgsqlConnection.ClearAllPools();

            _client = null;
            _factory = null;
        }

        private protected async Task<TokenPairResponseDto> LoginAsync(string username = "testuser", string password = "testpassword")
        {
            EnsureClientCreated();

            HttpResponseMessage response = await LoginRawAsync(username, password);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(payload, Is.Not.Null);
            return payload!;
        }

        private protected async Task<HttpResponseMessage> LoginRawAsync(string username, string password)
        {
            EnsureClientCreated();

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = username,
                    Password = password
                })
            };

            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            return await _client!.SendAsync(request);
        }

        private protected async Task<JsonElement> GetJsonAsync(string url)
        {
            EnsureClientCreated();

            JsonElement response = await _client!.GetFromJsonAsync<JsonElement>(url);
            return response;
        }

        private protected void SetBearer(string accessToken)
        {
            EnsureClientCreated();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private protected void EnsureClientCreated()
        {
            if (_client is not null)
            {
                return;
            }

            _client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }
    }
}
