// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class NotificationBatchEndpointsTests : IntegrationTestBase
    {
        private const string Username = "testuser";
        private const string Password = "testpassword";

        private TestAppFactory? _factory;
        private HttpClient? _client;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            _factory = new TestAppFactory(CreateOverrides());
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task GetNotificationBatch_WithoutCursor_ReturnsExactCappedUnreadBacklogAndOwnerWatermark()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);
            await CreateUserAsync("otheruser", "otherpass");
            Guid otherOwnerId = await GetUserIdAsync("otheruser");
            await DeleteNotificationsAsync(ownerId);
            await DeleteNotificationsAsync(otherOwnerId);
            DateTime createdAt = new(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc);

            await AddNotificationAsync(ownerId, createdAt, "oldest unread");
            Guid middleId = await AddNotificationAsync(ownerId, createdAt.AddMinutes(1), "middle unread");
            Guid newestUnreadId = await AddNotificationAsync(ownerId, createdAt.AddMinutes(2), "newest unread");
            Guid ownerWatermarkId = await AddNotificationAsync(
                ownerId,
                createdAt.AddMinutes(3),
                "already read",
                readAt: createdAt.AddMinutes(4));
            await AddNotificationAsync(
                otherOwnerId,
                createdAt.AddMinutes(5),
                "other user unread");

            NotificationBatchDto response = await GetBatchAsync(cursor: null, detailLimit: 2);

            Assert.Multiple(() =>
            {
                Assert.That(response.UnreadCount, Is.EqualTo(3));
                Assert.That(
                    response.UnreadNotifications.Select(notification => notification.Id),
                    Is.EqualTo(new[] { newestUnreadId, middleId }));
                Assert.That(response.UnreadNotifications.All(notification => notification.UserId == ownerId), Is.True);
                Assert.That(response.NextCursor?.CreatedAt, Is.EqualTo(createdAt.AddMinutes(3)));
                Assert.That(response.NextCursor?.NotificationId, Is.EqualTo(ownerWatermarkId));
            });
        }

        [Test]
        public async Task GetNotificationBatch_WithEqualTimestamps_UsesIdentifierTieBreaker()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);
            await DeleteNotificationsAsync(ownerId);
            DateTime createdAt = new(2026, 8, 12, 19, 0, 0, DateTimeKind.Utc);
            await AddNotificationAsync(ownerId, createdAt, "equal timestamp one");
            await AddNotificationAsync(ownerId, createdAt, "equal timestamp two");
            await AddNotificationAsync(ownerId, createdAt, "equal timestamp three");
            List<Guid> notificationIds = await GetOrderedNotificationIdsAsync(ownerId);
            Guid cursorId = notificationIds[1];
            Guid higherId = notificationIds[2];
            NotificationCursorDto cursor = new()
            {
                CreatedAt = createdAt,
                NotificationId = cursorId,
            };

            NotificationBatchDto response = await GetBatchAsync(cursor, detailLimit: 10);

            Assert.Multiple(() =>
            {
                Assert.That(response.UnreadCount, Is.EqualTo(1));
                Assert.That(response.UnreadNotifications.Single().Id, Is.EqualTo(higherId));
                Assert.That(response.NextCursor?.CreatedAt, Is.EqualTo(createdAt));
                Assert.That(response.NextCursor?.NotificationId, Is.EqualTo(higherId));
            });
        }

        [Test]
        public async Task GetNotificationBatch_WhenOnlyNewNotificationIsRead_AdvancesCursorWithoutUnreadDetails()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);
            await DeleteNotificationsAsync(ownerId);
            DateTime createdAt = new(2026, 8, 12, 20, 0, 0, DateTimeKind.Utc);
            Guid cursorId = await AddNotificationAsync(ownerId, createdAt, "cursor notification");
            Guid readNotificationId = await AddNotificationAsync(
                ownerId,
                createdAt.AddMinutes(1),
                "read elsewhere",
                readAt: createdAt.AddMinutes(2));
            NotificationCursorDto cursor = new()
            {
                CreatedAt = createdAt,
                NotificationId = cursorId,
            };

            NotificationBatchDto response = await GetBatchAsync(cursor, detailLimit: 10);

            Assert.Multiple(() =>
            {
                Assert.That(response.UnreadCount, Is.Zero);
                Assert.That(response.UnreadNotifications, Is.Empty);
                Assert.That(response.NextCursor?.CreatedAt, Is.EqualTo(createdAt.AddMinutes(1)));
                Assert.That(response.NextCursor?.NotificationId, Is.EqualTo(readNotificationId));
            });
        }

        [TestCase("cursorCreatedAt=2026-08-12T20%3A00%3A00.0000000Z")]
        [TestCase("cursorNotificationId=00000000-0000-0000-0000-000000000001")]
        [TestCase("detailLimit=0")]
        public async Task GetNotificationBatch_WithInvalidQuery_ReturnsBadRequest(string query)
        {
            await SignInAsync();

            using HttpResponseMessage response = await _client!.GetAsync(
                $"{Routes.V1.Notifications}/batch?{query}");

            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        }

        private Dictionary<string, string?> CreateOverrides()
        {
            NpgsqlConnectionStringBuilder connectionString = new()
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Database = CurrentDatabaseName,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword,
            };

            return new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = connectionString.Host,
                ["DatabaseSettings:Port"] = connectionString.Port.ToString(CultureInfo.InvariantCulture),
                ["DatabaseSettings:Database"] = connectionString.Database,
                ["DatabaseSettings:Username"] = connectionString.Username,
                ["DatabaseSettings:Password"] = connectionString.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };
        }

        private async Task SignInAsync()
        {
            using HttpRequestMessage request = new(HttpMethod.Post, $"{Routes.V1.Auth}/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = Username,
                    Password = Password,
                }),
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");

            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();
            TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        }

        private async Task<Guid> GetUserIdAsync(string username)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            User user = await dbContext.Users
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Username == username);
            return user.Id;
        }

        private async Task CreateUserAsync(string username, string password)
        {
            using HttpResponseMessage response = await _client!.PostAsJsonAsync(Routes.V1.Users, new
            {
                username,
                password,
                role = UserRole.User,
            });
            response.EnsureSuccessStatusCode();
        }

        private async Task<Guid> AddNotificationAsync(
            Guid userId,
            DateTime createdAt,
            string title,
            DateTime? readAt = null)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Notification notification = new()
            {
                UserId = userId,
                Title = title,
                Priority = NotificationPriority.None,
                ReadAt = readAt,
            };
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();
            await dbContext.Notifications
                .Where(candidate => candidate.Id == notification.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.CreatedAt, createdAt)
                    .SetProperty(candidate => candidate.UpdatedAt, createdAt));
            return notification.Id;
        }

        private async Task DeleteNotificationsAsync(Guid userId)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            await dbContext.Notifications
                .Where(notification => notification.UserId == userId)
                .ExecuteDeleteAsync();
        }

        private async Task<List<Guid>> GetOrderedNotificationIdsAsync(Guid userId)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            return await dbContext.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId)
                .OrderBy(notification => notification.Id)
                .Select(notification => notification.Id)
                .ToListAsync();
        }

        private async Task<NotificationBatchDto> GetBatchAsync(
            NotificationCursorDto? cursor,
            int detailLimit)
        {
            string path = $"{Routes.V1.Notifications}/batch?detailLimit={detailLimit}";
            if (cursor is not null)
            {
                string cursorCreatedAt = Uri.EscapeDataString(
                    cursor.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                path += $"&cursorCreatedAt={cursorCreatedAt}&cursorNotificationId={cursor.NotificationId:D}";
            }

            NotificationBatchDto? response = await _client!.GetFromJsonAsync<NotificationBatchDto>(path);
            Assert.That(response, Is.Not.Null);
            return response!;
        }
    }
}
