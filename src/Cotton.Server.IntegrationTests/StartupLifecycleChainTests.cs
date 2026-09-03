// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class StartupLifecycleChainTests : IntegrationTestBase
    {
        private const string PreRestoredMigrationId = "20260427214223_AddCustomGeoIpLookupUrl";
        private const string RestoredMigrationTailId = "20260516005639_DropNodeFilesNameKeyUniqueness";

        private TestAppFactory? _factory;
        private HttpClient? _client;

        public StartupLifecycleChainTests()
            : base("cotton_dev_tests_startup_" + Guid.NewGuid().ToString("N"))
        {
        }

        private record IsServerInitializedResponse(bool IsServerInitialized);
        private record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail, string? Instance);

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

            _factory = new TestAppFactory(overrides);
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

        [Test]
        public async Task Startup_OnCleanDatabase_AppliesMigrations_AndCreatesInitialAdminWithinWindow()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            Assert.That(
                await creator.HasTablesAsync(),
                Is.False,
                "DB should start with no user tables in this test setup.");

            TokenPairResponseDto login = await LoginAsync();
            Assert.That(login.AccessToken, Is.Not.Null.And.Not.Empty);

            Assert.That(
                await creator.HasTablesAsync(),
                Is.True,
                "Server startup should apply migrations automatically.");

            SetBearer(login.AccessToken);
            UserDto? me = await _client!.GetFromJsonAsync<UserDto>("/api/v1/users/me");
            Assert.That(me, Is.Not.Null);
            Assert.That(me!.Username, Is.EqualTo("testuser"));
            Assert.That(me.Role, Is.EqualTo((int)UserRole.Admin), "First user should be admin on non-public instance.");
        }

        [Test]
        public async Task Startup_FromPreRestoreDatabase_AppliesRestoredMigrationTrail()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            Assert.That(
                await creator.HasTablesAsync(),
                Is.False,
                "DB should start with no user tables in this test setup.");

            await DbContext.GetService<IMigrator>().MigrateAsync(PreRestoredMigrationId);
            NpgsqlConnection.ClearAllPools();

            Assert.That(await MigrationAppliedAsync(PreRestoredMigrationId), Is.True);
            Assert.That(await MigrationAppliedAsync(RestoredMigrationTailId), Is.False);
            Assert.That(await ColumnExistsAsync("node_files", "is_client_encrypted"), Is.True);
            Assert.That(await ColumnExistsAsync("nodes", "metadata"), Is.False);
            NpgsqlConnection.ClearAllPools();

            TokenPairResponseDto login = await LoginAsync();
            Assert.That(login.AccessToken, Is.Not.Null.And.Not.Empty);

            Assert.That(await MigrationAppliedAsync(RestoredMigrationTailId), Is.True);
            Assert.That(await ColumnExistsAsync("nodes", "metadata"), Is.True);
            Assert.That(await ColumnExistsAsync("node_files", "is_client_encrypted"), Is.False);
            Assert.That(
                await IndexExistsAsync("IX_node_files_node_id_name_key_owner_id_id"),
                Is.True);
        }

        [Test]
        public async Task Login_ForUnknownUser_AfterAdminExists_ReturnsUnauthorized()
        {
            await LoginAsync();

            HttpResponseMessage secondLogin = await LoginRawAsync("new-user", "some-password");
            Assert.That(secondLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task FirstSettingsPatch_CreatesSafeDefaults_AndSetsSetupCompleteFlag()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            bool before = await GetIsServerInitializedAsync();
            Assert.That(before, Is.False);

            HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/telemetry",
                false);
            response.EnsureSuccessStatusCode();

            bool after = await GetIsServerInitializedAsync();
            Assert.That(after, Is.True);

            JsonElement publicBaseUrl = await GetJsonAsync("/api/v1/server/settings/public-base-url");
            Assert.That(publicBaseUrl.GetProperty("publicBaseUrl").GetString(), Does.Contain("localhost"));
        }

        [Test]
        public async Task SettingsPatchFlow_PersistsIndependentConfigurationSteps()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/allow-cross-user-deduplication", true)).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/allow-global-indexing", true)).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/server-usage", new[] { "Photos", "Documents" })).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/telemetry", true)).EnsureSuccessStatusCode();
            (await _client!.PatchAsync("/api/v1/server/settings/compution-mode/Local", null)).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/timezone", "UTC")).EnsureSuccessStatusCode();
            (await _client!.PatchAsync("/api/v1/server/settings/storage-space-mode/Limited", null)).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/public-base-url", "https://cotton.example/")).EnsureSuccessStatusCode();
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/custom-geoip-lookup-url", "https://geo.example/lookup/{ip}")).EnsureSuccessStatusCode();
            (await _client!.PatchAsync("/api/v1/server/settings/geoip-lookup-mode/CustomHttp", null)).EnsureSuccessStatusCode();

            EmailConfig emailConfig = new EmailConfig
            {
                SmtpServer = "smtp.example.com",
                Port = "587",
                Username = "mailer",
                Password = "secret",
                FromAddress = "noreply@example.com",
                UseSSL = true
            };
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/email-config", emailConfig)).EnsureSuccessStatusCode();
            (await _client!.PatchAsync("/api/v1/server/settings/email-mode/Custom", null)).EnsureSuccessStatusCode();

            Assert.That(await GetIsServerInitializedAsync(), Is.True);

            JsonElement publicBaseUrl = await GetJsonAsync("/api/v1/server/settings/public-base-url");
            JsonElement serverUsage = await GetJsonAsync("/api/v1/server/settings/server-usage");
            JsonElement geoIpMode = await GetJsonAsync("/api/v1/server/settings/geoip-lookup-mode");
            JsonElement emailMode = await GetJsonAsync("/api/v1/server/settings/email-mode");
            JsonElement storedEmailConfig = await GetJsonAsync("/api/v1/server/settings/email-config");

            Assert.Multiple(() =>
            {
                Assert.That(publicBaseUrl.GetProperty("publicBaseUrl").GetString(), Is.EqualTo("https://cotton.example"));
                Assert.That(serverUsage.GetProperty("serverUsage").EnumerateArray().Select(x => x.GetString()), Does.Contain("Photos"));
                Assert.That(geoIpMode.GetProperty("geoIpLookupMode").GetString(), Is.EqualTo("CustomHttp"));
                Assert.That(emailMode.GetProperty("emailMode").GetString(), Is.EqualTo("Custom"));
                Assert.That(storedEmailConfig.GetProperty("smtpServer").GetString(), Is.EqualTo("smtp.example.com"));
                Assert.That(storedEmailConfig.GetProperty("password").GetString(), Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public async Task SettingsPatch_Rejects_InvalidTimezone_ButKeepsSafeDefaults()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            bool before = await GetIsServerInitializedAsync();
            Assert.That(before, Is.False);

            HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/timezone",
                "Mars/OlympusMons");

            await AssertBadRequestProblemDetailsAsync(
                response,
                "/api/v1/server/settings/timezone",
                "Timezone not found: Mars/OlympusMons");
            Assert.That(await GetIsServerInitializedAsync(), Is.True);
        }

        [Test]
        public async Task SettingsPatch_Rejects_CloudEmail_WithoutTelemetry()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            HttpResponseMessage response = await _client!.PatchAsync("/api/v1/server/settings/email-mode/Cloud", null);

            await AssertBadRequestProblemDetailsAsync(
                response,
                "/api/v1/server/settings/email-mode/Cloud",
                "Telemetry must be enabled to use Cotton Bridge Mail.");
        }

        [Test]
        public async Task SettingsPatch_Rejects_CloudComputation_WithoutTelemetry()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            HttpResponseMessage response = await _client!.PatchAsync("/api/v1/server/settings/compution-mode/Cloud", null);

            await AssertBadRequestProblemDetailsAsync(
                response,
                "/api/v1/server/settings/compution-mode/Cloud",
                "Telemetry must be enabled to use Cotton Bridge AI.");
        }

        [Test]
        public async Task SettingsPatch_Rejects_CustomEmail_WithoutEmailConfig()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            HttpResponseMessage response = await _client!.PatchAsync("/api/v1/server/settings/email-mode/Custom", null);

            await AssertBadRequestProblemDetailsAsync(
                response,
                "/api/v1/server/settings/email-mode/Custom",
                "SMTP settings must be configured before enabling Custom email service.");
        }

        [Test]
        public async Task SettingsPatch_Rejects_S3Storage_WithoutS3Config()
        {
            TokenPairResponseDto login = await LoginAsync();
            SetBearer(login.AccessToken);

            HttpResponseMessage response = await _client!.PatchAsync("/api/v1/server/settings/storage-type/S3", null);

            await AssertBadRequestProblemDetailsAsync(
                response,
                "/api/v1/server/settings/storage-type/S3",
                "S3 settings must be configured before enabling S3 storage.");
        }

        private async Task<TokenPairResponseDto> LoginAsync(string username = "testuser", string password = "testpassword")
        {
            EnsureClientCreated();

            HttpResponseMessage response = await LoginRawAsync(username, password);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(payload, Is.Not.Null);
            return payload!;
        }

        private async Task<HttpResponseMessage> LoginRawAsync(string username, string password)
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

        private async Task<bool> GetIsServerInitializedAsync()
        {
            EnsureClientCreated();

            IsServerInitializedResponse? response = await _client!.GetFromJsonAsync<IsServerInitializedResponse>("/api/v1/server/settings/is-setup-complete");
            Assert.That(response, Is.Not.Null);
            return response!.IsServerInitialized;
        }

        private async Task<JsonElement> GetJsonAsync(string url)
        {
            EnsureClientCreated();

            JsonElement response = await _client!.GetFromJsonAsync<JsonElement>(url);
            return response;
        }

        private async Task<bool> MigrationAppliedAsync(string migrationId)
        {
            IEnumerable<string> appliedMigrations = await DbContext.Database.GetAppliedMigrationsAsync();
            return appliedMigrations.Contains(migrationId, StringComparer.Ordinal);
        }

        private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            await using NpgsqlConnection connection = await OpenSchemaConnectionAsync();
            System.Data.DataTable columns = await connection.GetSchemaAsync("Columns");
            return columns.Rows.Cast<System.Data.DataRow>().Any(row =>
                string.Equals(row["table_schema"] as string, "public", StringComparison.Ordinal)
                && string.Equals(row["table_name"] as string, tableName, StringComparison.Ordinal)
                && string.Equals(row["column_name"] as string, columnName, StringComparison.Ordinal));
        }

        private async Task<bool> IndexExistsAsync(string indexName)
        {
            await using NpgsqlConnection connection = await OpenSchemaConnectionAsync();
            System.Data.DataTable indexes = await connection.GetSchemaAsync("Indexes");
            return indexes.Rows.Cast<System.Data.DataRow>().Any(row =>
                string.Equals(row["table_schema"] as string, "public", StringComparison.Ordinal)
                && string.Equals(row["index_name"] as string, indexName, StringComparison.Ordinal));
        }

        private async Task<NpgsqlConnection> OpenSchemaConnectionAsync()
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Missing test database connection string.");
            NpgsqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        private void SetBearer(string accessToken)
        {
            EnsureClientCreated();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private static async Task AssertBadRequestProblemDetailsAsync(
            HttpResponseMessage response,
            string expectedInstance,
            string expectedDetail)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            ProblemDetailsResponse? payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.Status, Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(payload.Title, Is.EqualTo("Bad Request"));
            Assert.That(payload.Detail, Is.EqualTo(expectedDetail));
            Assert.That(payload.Instance, Is.EqualTo(expectedInstance));
        }

        private void EnsureClientCreated()
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
