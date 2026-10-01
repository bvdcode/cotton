// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SyncChangesFeedAndRetentionTests : SyncChangesTestBase
    {
        [Test]
        public async Task GetChanges_WhenFeedIsEmpty_ReturnsEmptyPage()
        {
            await SignInAsync();

            SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

            Assert.Multiple(() =>
            {
                Assert.That(response.SinceCursor, Is.EqualTo(0));
                Assert.That(response.NextCursor, Is.EqualTo(0));
                Assert.That(response.HasMore, Is.False);
                Assert.That(response.Changes, Is.Empty);
            });
        }

        [Test]
        public async Task GetChanges_ReturnsOrderedCurrentUserPageAfterCursor()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);
            await CreateUserAsync("otheruser", "otherpass");
            Guid otherOwnerId = await GetUserIdAsync("otheruser");

            long firstOwnerChangeId = await AddSyncChangeAsync(ownerId, "ignored-before-cursor");
            await AddSyncChangeAsync(otherOwnerId, "other-user");
            long includedChangeId = await AddSyncChangeAsync(ownerId, "included");
            await AddSyncChangeAsync(ownerId, "next-page");

            SyncChangesResponseDto response = await GetChangesAsync(since: firstOwnerChangeId, limit: 1);

            Assert.Multiple(() =>
            {
                Assert.That(response.SinceCursor, Is.EqualTo(firstOwnerChangeId));
                Assert.That(response.NextCursor, Is.EqualTo(includedChangeId));
                Assert.That(response.HasMore, Is.True);
                Assert.That(response.Changes, Has.Count.EqualTo(1));
                Assert.That(response.Changes[0].Id, Is.EqualTo(includedChangeId));
                Assert.That(response.Changes[0].Name, Is.EqualTo("included"));
                Assert.That(response.Changes[0].Kind, Is.EqualTo(SyncChangeKind.FileCreated));
            });
        }

        [Test]
        public async Task GetChanges_WhenCursorIsNegative_ReturnsBadRequest()
        {
            await SignInAsync();

            using HttpResponseMessage response = await _client!.GetAsync($"{Routes.V1.Sync}/changes?since=-1&limit=10");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task GetChanges_WhenCursorIsOlderThanRetainedFeed_ReturnsExpiredCursor()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);

            long expiredId = await AddSyncChangeAsync(ownerId, "expired");
            long retainedId = await AddSyncChangeAsync(ownerId, "retained");
            await DeleteSyncChangeAsync(expiredId);

            SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

            Assert.Multiple(() =>
            {
                Assert.That(response.CursorExpired, Is.True);
                Assert.That(response.SinceCursor, Is.EqualTo(0));
                Assert.That(response.NextCursor, Is.EqualTo(0));
                Assert.That(response.EarliestAvailableCursor, Is.EqualTo(retainedId - 1));
                Assert.That(response.Changes, Is.Empty);
            });
        }

        [Test]
        public async Task RetentionJob_KeepsNewestExpiredChangeAsCursorMarker()
        {
            await SignInAsync();
            Guid ownerId = await GetUserIdAsync(Username);

            DateTime cutoff = DateTime.UtcNow.AddDays(-365);
            long oldestExpiredId = await AddSyncChangeAsync(ownerId, "oldest-expired");
            long markerId = await AddSyncChangeAsync(ownerId, "cursor-marker");
            await SetSyncChangeCreatedAtAsync(oldestExpiredId, cutoff.AddDays(-2));
            await SetSyncChangeCreatedAtAsync(markerId, cutoff.AddDays(-1));

            int deletedCount = await RunSyncRetentionAsync(cutoff);

            List<long> remainingIds = await GetSyncChangeIdsAsync(ownerId);
            SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

            Assert.Multiple(() =>
            {
                Assert.That(deletedCount, Is.EqualTo(1));
                Assert.That(remainingIds, Is.EqualTo(new[] { markerId }));
                Assert.That(response.CursorExpired, Is.True);
                Assert.That(response.EarliestAvailableCursor, Is.EqualTo(markerId - 1));
                Assert.That(response.Changes, Is.Empty);
            });
        }

        private async Task<Guid> GetUserIdAsync(string username)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            User user = await dbContext.Users
                .AsNoTracking()
                .SingleAsync(x => x.Username == username);

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

        private async Task<long> AddSyncChangeAsync(Guid ownerId, string name)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            SyncChange change = new SyncChange
            {
                OwnerId = ownerId,
                Kind = SyncChangeKind.FileCreated,
                LayoutId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ParentNodeId = Guid.NewGuid(),
                Name = name,
            };

            dbContext.SyncChanges.Add(change);
            await dbContext.SaveChangesAsync();
            return change.Id;
        }

        private async Task DeleteSyncChangeAsync(long id)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            await dbContext.SyncChanges
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();
        }

        private async Task SetSyncChangeCreatedAtAsync(long id, DateTime createdAt)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            await dbContext.SyncChanges
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(change => change.CreatedAt, createdAt)
                    .SetProperty(change => change.UpdatedAt, createdAt));
        }

        private async Task<int> RunSyncRetentionAsync(DateTime cutoff)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            SyncChangeRetentionJob job = new SyncChangeRetentionJob(
                dbContext,
                NullLogger<SyncChangeRetentionJob>.Instance);

            return await job.DeleteExpiredChangesAsync(cutoff, CancellationToken.None);
        }

        private async Task<List<long>> GetSyncChangeIdsAsync(Guid ownerId)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            return await dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();
        }
    }
}
