// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Models.DatabaseBackup;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseBackupEndpointsTests : AuthEndpointTestBase
    {
        private const string Route = "/api/v1/server/database-backup";

        [Test]
        public async Task BackupToken_IsRequiredForExternalTrigger_AndDoesNotGrantAdminAccess()
        {
            using HttpResponseMessage anonymous = await _client!.PostAsync(Route, null);
            Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            _client.DefaultRequestHeaders.Add(DatabaseBackupAuthenticationHandler.HeaderName, "invalid");
            using HttpResponseMessage invalid = await _client.PostAsync(Route, null);
            Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            _client.DefaultRequestHeaders.Remove(DatabaseBackupAuthenticationHandler.HeaderName);
            using HttpResponseMessage anonymousIssue = await _client.PostAsync(Route + "/token", null);
            Assert.That(anonymousIssue.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            string token = await IssueTokenAsync();
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add(DatabaseBackupAuthenticationHandler.HeaderName, token);
            using HttpResponseMessage issue = await _client.PostAsync(Route + "/token", null);
            using HttpResponseMessage history = await _client.GetAsync(Route);
            using HttpResponseMessage trigger = await _client.PatchAsync(Route + "/trigger", null);
            Assert.Multiple(() =>
            {
                Assert.That(issue.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(history.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(trigger.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CreateAndRestore_PreservesHistory_AndNotifiesAdmins(bool missingLatest)
        {
            string executable = OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";
            if (!(Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Any(directory => File.Exists(Path.Combine(directory, executable))))
            {
                Assert.Ignore("PostgreSQL dump tools must be available on PATH.");
            }
            string token = await IssueTokenAsync();
            AuthenticationHeaderValue? adminAuthorization = _client!.DefaultRequestHeaders.Authorization;
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add(DatabaseBackupAuthenticationHandler.HeaderName, token);
            List<LatestDatabaseBackupDto> completed = [];
            for (int index = 0; index < 2; index++)
            {
                using HttpResponseMessage response = await _client.PostAsync(Route, null);
                response.EnsureSuccessStatusCode();
                LatestDatabaseBackupDto backup = (await response.Content.ReadFromJsonAsync<LatestDatabaseBackupDto>())!;
                completed.Insert(0, backup);
                await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
                IDatabaseBackupManifestService manifests = scope.ServiceProvider.GetRequiredService<IDatabaseBackupManifestService>();
                Models.DatabaseBackup.ResolvedBackupManifest? saved = await manifests.TryGetLatestManifestAsync();
                Assert.That(saved!.Manifest.BackupId, Is.EqualTo(backup.BackupId));
                Assert.That(backup.DumpSizeBytes, Is.GreaterThan(0));
            }
            _client.DefaultRequestHeaders.Authorization = adminAuthorization;
            LatestDatabaseBackupDto[] history = (await _client.GetFromJsonAsync<LatestDatabaseBackupDto[]>(Route))!;
            Assert.That(history.Select(item => item.BackupId), Is.EqualTo(completed.Select(item => item.BackupId)));
            await VerifyRestoreAsync(completed, missingLatest);
        }

        private async Task VerifyRestoreAsync(List<LatestDatabaseBackupDto> completed, bool missingLatest)
        {
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            IStoragePipeline storage = scope.ServiceProvider.GetRequiredService<IStoragePipeline>();
            IDatabaseBackupManifestService manifests = scope.ServiceProvider.GetRequiredService<IDatabaseBackupManifestService>();
            BackupManifestPointer pointer = (await manifests.ReadPointerAsync())!;
            if (missingLatest)
            {
                await storage.DeleteAsync(pointer.LatestManifestStorageKey);
            }
            string databaseName = $"cotton_backup_restore_tests_{Guid.NewGuid():N}";
            NpgsqlConnectionStringBuilder connection = new(DbContext.Database.GetConnectionString()) { Database = databaseName };
            await using CottonDbContext restored = new(new DbContextOptionsBuilder<CottonDbContext>().UseNpgsql(connection.ConnectionString).Options);
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["COTTON_RESTORE_DATABASE_IF_EMPTY"] = "true",
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["DatabaseSettings:Database"] = databaseName,
            }).Build();
            RecordingNotificationsProvider notifications = new();
            DatabaseBackupRestorePreparation preparation = new(manifests, storage, NullLogger<DatabaseBackupRestorePreparation>.Instance);
            DatabaseAutoRestoreService restore = new(configuration, restored, preparation,
                new PostgresDumpService(configuration, NullLogger<PostgresDumpService>.Instance), manifests, notifications,
                NullLogger<DatabaseAutoRestoreService>.Instance);
            try
            {
                await restored.Database.EnsureCreatedAsync();
                await restore.TryRestoreIfEmptyAsync();
                Assert.That(await restored.Users.AnyAsync(user => user.Username == "testuser"), Is.True);
                Assert.That(notifications.Notifications, Has.Count.EqualTo(1));
                var (_, priority, metadata) = notifications.Notifications.Single();
                Assert.That(priority, Is.EqualTo(NotificationPriority.High));
                Assert.That(metadata!["backupId"], Is.EqualTo(completed[missingLatest ? 1 : 0].BackupId));
                Assert.That(metadata["skippedBackupIds"], Is.EqualTo(missingLatest ? completed[0].BackupId : ""));
                Assert.That(metadata["i18n.contentKey"], Is.EqualTo(missingLatest
                    ? NotificationTemplateKeys.DatabaseRestoreCompletedWithFallbackContent
                    : NotificationTemplateKeys.DatabaseRestoreCompletedContent));
            }
            finally
            {
                await restored.Database.EnsureDeletedAsync();
            }
        }

        private async Task<string> IssueTokenAsync()
        {
            AuthSessionResponseDto session = await LoginAsync("testuser", "testpassword");
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            using HttpResponseMessage response = await _client.PostAsync(Route + "/token", null);
            response.EnsureSuccessStatusCode();
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
            return (await response.Content.ReadFromJsonAsync<DatabaseBackupTokenDto>())!.Token;
        }
    }
}
