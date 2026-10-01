// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Hosting;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class AuthEndpointTestBase : IntegrationTestBase
    {
        private protected TestAppFactory? _factory;
        private protected WebApplicationFactory<Program>? _customFactory;
        private protected HttpClient? _client;
        private protected RecordingNotificationsProvider? _notifications;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();
            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True, "DB must exist after Create()");
                Assert.That(creator.HasTables(), Is.False, "DB must have no user tables after Create()");
            });

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Database = CurrentDatabaseName,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword
            };

            Dictionary<string, string?> overrides = new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = csb.Host,
                ["DatabaseSettings:Port"] = csb.Port.ToString(),
                ["DatabaseSettings:Database"] = csb.Database,
                ["DatabaseSettings:Username"] = csb.Username,
                ["DatabaseSettings:Password"] = csb.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF")),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
            };

            _factory = new TestAppFactory(overrides);
            RecordingNotificationsProvider notifications = new();
            _notifications = notifications;
            _customFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == typeof(IStoragePipeline));
                    if (existing is not null)
                    {
                        services.Remove(existing);
                    }
                    services.AddSingleton<IStoragePipeline, InMemoryStorage>();

                    ServiceDescriptor? existingNotifications = services
                        .FirstOrDefault(d => d.ServiceType == typeof(INotificationsProvider));
                    if (existingNotifications is not null)
                    {
                        services.Remove(existingNotifications);
                    }
                    services.AddSingleton<INotificationsProvider>(notifications);
                });
                builder.ConfigureLogging((ctx, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new NUnitLoggerProvider());
                    logging.SetMinimumLevel(LogLevel.Information);
                });
            });

            _client = _customFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _customFactory?.Dispose();
            _factory?.Dispose();
        }

        private protected async Task<AuthSessionResponseDto> LoginAsync(string username, string password)
        {
            using HttpResponseMessage response = await PostLoginAsync(username, password, "8.8.8.8");
            response.EnsureSuccessStatusCode();

            AuthSessionResponseDto? payload = await response.Content.ReadFromJsonAsync<AuthSessionResponseDto>();
            Assert.That(payload, Is.Not.Null);
            return payload!;
        }

        private protected Task<HttpResponseMessage> PostLoginAsync(
            string username,
            string password,
            string ipAddress,
            string? deviceName = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = username,
                    Password = password
                })
            };
            request.Headers.Add("X-Forwarded-For", ipAddress);
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                request.Headers.Add(CottonClientHeaders.DeviceName, deviceName);
            }

            return _client!.SendAsync(request);
        }

        private protected async Task<int> GetTotpFailedAttemptsAsync(string username)
        {
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            return await dbContext.Users
                .Where(user => user.Username == username)
                .Select(user => user.TotpFailedAttempts)
                .SingleAsync();
        }
    }
}
