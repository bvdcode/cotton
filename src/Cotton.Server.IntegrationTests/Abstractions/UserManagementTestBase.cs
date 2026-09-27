// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using System.Net.Http.Headers;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class UserManagementTestBase : IntegrationTestBase
    {
        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;

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

            _factory = new TestAppFactory(overrides);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private protected async Task<UserDto> CreateUserAsync(string username, string email)
        {
            HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Username = username,
                    Email = email,
                    Password = "UserPass_123",
                    Role = UserRole.User
                });

            createResponse.EnsureSuccessStatusCode();

            UserDto? user = await createResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.That(user, Is.Not.Null);
            return user!;
        }

        private protected async Task<string> LoginAsync(
            string username = "testuser",
            string password = "testpassword")
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = username,
                    Password = password
                })
            };

            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            return login!.AccessToken;
        }

        private protected void SetBearer(string token)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
